using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FantasyDraftAssistant.Core.Interfaces;
using FantasyDraftAssistant.Data.Database;

namespace FantasyDraftAssistant.Data.Services;

public sealed class FileCredentialStore : ICredentialStore
{
    private readonly string _path;
    private readonly byte[] _key;

    public FileCredentialStore(AppPaths paths)
    {
        _path = paths.CredentialPath;
        _key = DeriveKey();
    }

    public bool IsSecure => false;
    public string Description => "File-based fallback credential store. OS secure storage was not available.";

    public Task SaveSecretAsync(string scope, string key, string secret, CancellationToken cancellationToken = default)
    {
        var map = Load();
        map[$"{scope}:{key}"] = secret;
        Save(map);
        return Task.CompletedTask;
    }

    public Task<string?> GetSecretAsync(string scope, string key, CancellationToken cancellationToken = default)
    {
        var map = Load();
        map.TryGetValue($"{scope}:{key}", out var secret);
        return Task.FromResult(secret);
    }

    public Task DeleteSecretAsync(string scope, string key, CancellationToken cancellationToken = default)
    {
        var map = Load();
        map.Remove($"{scope}:{key}");
        Save(map);
        return Task.CompletedTask;
    }

    private Dictionary<string, string> Load()
    {
        if (!File.Exists(_path))
            return new Dictionary<string, string>();
        try
        {
            var blob = File.ReadAllBytes(_path);
            var plain = Unprotect(blob);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(plain) ?? new Dictionary<string, string>();
        }
        catch (CryptographicException)
        {
            return new Dictionary<string, string>();
        }
    }

    private void Save(Dictionary<string, string> map)
    {
        var json = JsonSerializer.Serialize(map);
        File.WriteAllBytes(_path, Protect(Encoding.UTF8.GetBytes(json)));
    }

    private byte[] Protect(byte[] data)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var cipher = new byte[data.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(_key, 16);
        aes.Encrypt(nonce, data, cipher, tag);
        return nonce.Concat(tag).Concat(cipher).ToArray();
    }

    private byte[] Unprotect(byte[] blob)
    {
        var nonce = blob[..12];
        var tag = blob[12..28];
        var cipher = blob[28..];
        var plain = new byte[cipher.Length];
        using var aes = new AesGcm(_key, 16);
        aes.Decrypt(nonce, cipher, tag, plain);
        return plain;
    }

    private static byte[] DeriveKey()
    {
        var seed = $"{Environment.UserName}|{Environment.MachineName}|fantasy-draft-assistant";
        return SHA256.HashData(Encoding.UTF8.GetBytes(seed));
    }
}

public sealed class LinuxSecretToolCredentialStore : ICredentialStore
{
    private readonly ICredentialStore _fallback;

    public LinuxSecretToolCredentialStore(ICredentialStore fallback)
    {
        _fallback = fallback;
        IsSecure = CanUseSecretTool();
        Description = IsSecure
            ? "Linux Secret Service via secret-tool."
            : fallback.Description;
    }

    public bool IsSecure { get; }
    public string Description { get; }

    public async Task SaveSecretAsync(string scope, string key, string secret, CancellationToken cancellationToken = default)
    {
        if (!IsSecure)
        {
            await _fallback.SaveSecretAsync(scope, key, secret, cancellationToken);
            return;
        }

        await ClearSecretToolAsync(scope, key, cancellationToken);

        var psi = new System.Diagnostics.ProcessStartInfo("secret-tool", $"store --label=FantasyDraftAssistant/{scope}/{key} app fantasy-draft-assistant scope {scope} key {key}")
        {
            RedirectStandardInput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        using var process = System.Diagnostics.Process.Start(psi)
                            ?? throw new InvalidOperationException("Failed to start secret-tool.");
        await process.StandardInput.WriteAsync(secret);
        process.StandardInput.Close();
        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0)
            await _fallback.SaveSecretAsync(scope, key, secret, cancellationToken);
    }

    public async Task<string?> GetSecretAsync(string scope, string key, CancellationToken cancellationToken = default)
    {
        if (!IsSecure)
            return await _fallback.GetSecretAsync(scope, key, cancellationToken);

        var psi = new System.Diagnostics.ProcessStartInfo("secret-tool", $"lookup app fantasy-draft-assistant scope {scope} key {key}")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        using var process = System.Diagnostics.Process.Start(psi);
        if (process is null)
            return await _fallback.GetSecretAsync(scope, key, cancellationToken);
        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return process.ExitCode == 0 ? output.TrimEnd('\n') : await _fallback.GetSecretAsync(scope, key, cancellationToken);
    }

    public async Task DeleteSecretAsync(string scope, string key, CancellationToken cancellationToken = default)
    {
        if (!IsSecure)
        {
            await _fallback.DeleteSecretAsync(scope, key, cancellationToken);
            return;
        }

        await ClearSecretToolAsync(scope, key, cancellationToken);
        await _fallback.DeleteSecretAsync(scope, key, cancellationToken);
    }

    private static async Task ClearSecretToolAsync(string scope, string key, CancellationToken cancellationToken)
    {
        var psi = new System.Diagnostics.ProcessStartInfo("secret-tool", $"clear app fantasy-draft-assistant scope {scope} key {key}")
        {
            RedirectStandardError = true,
            UseShellExecute = false
        };
        using var process = System.Diagnostics.Process.Start(psi);
        if (process is not null)
            await process.WaitForExitAsync(cancellationToken);
    }

    private static bool CanUseSecretTool()
    {
        try
        {
            // "secret-tool --version" is not a supported flag: it prints usage and
            // exits 2, so probing with it reported "no keyring" on every machine and
            // quietly sent all credentials to the machine-bound file store instead.
            // "search" exits 0 whether or not it matches, and 1 when no Secret
            // Service is reachable, which is the question actually being asked.
            var psi = new System.Diagnostics.ProcessStartInfo("secret-tool", "search app fantasy-draft-assistant")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            using var process = System.Diagnostics.Process.Start(psi);
            process?.WaitForExit(1000);
            return process is { ExitCode: 0 };
        }
        catch (Exception)
        {
            return false;
        }
    }
}
