using FantasyDraftAssistant.Core.Ai;
using FantasyDraftAssistant.Core.Ids;

namespace FantasyDraftAssistant.Core.Interfaces;

public interface ITeamPortraitStore
{
    string? ExistingPath(TeamId teamId);
    bool Exists(TeamId teamId);
    string? LastPrompt(TeamId teamId);
    Task SaveAsync(TeamId teamId, byte[] imageBytes, string? prompt = null, CancellationToken cancellationToken = default);
    string? CopyTo(TeamId teamId, string destinationPath);
    event EventHandler<TeamId>? Changed;
}

public interface IFileSavePicker
{
    Task<string?> PickSavePathAsync(string suggestedFileName, string defaultExtension, CancellationToken cancellationToken = default);
    Task<PickedFile?> PickOpenFileAsync(string title, IReadOnlyList<string> extensions, CancellationToken cancellationToken = default);
}

public sealed class PickedFile
{
    public required string FileName { get; init; }
    public required byte[] Bytes { get; init; }
}

public static class TeamPortraitFiles
{
    public static string SuggestedFileName(string teamName, string sourcePath)
    {
        var ext = Path.GetExtension(sourcePath);
        if (string.IsNullOrWhiteSpace(ext))
            ext = ".jpg";
        var name = string.Join("_", teamName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).Trim();
        if (string.IsNullOrWhiteSpace(name))
            name = "team";
        return name + ext;
    }
}

/// <summary>JPEG, PNG, or WebP, capped so a huge file cannot be imported as a team portrait.</summary>
public static class TeamPortraitImage
{
    public const int MaxBytes = 8 * 1024 * 1024;
    public static readonly string[] Extensions = [".jpg", ".jpeg", ".png", ".webp"];

    public static string ExtensionOrJpeg(byte[] bytes) => Identify(bytes) ?? ".jpg";

    public static string? Identify(byte[] bytes)
    {
        if (bytes.Length >= 8
            && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
            return ".png";
        if (bytes.Length >= 12
            && bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46
            && bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50)
            return ".webp";
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            return ".jpg";
        return null;
    }

    public static string? RejectReason(byte[] bytes, int maxBytes = MaxBytes)
    {
        if (bytes.Length == 0)
            return "That file is empty.";
        if (bytes.Length > maxBytes)
            return "That image is too large (max 8 MB).";
        if (Identify(bytes) is null)
            return "Use a JPEG, PNG, or WebP image.";
        return null;
    }
}

public interface ITeamPortraitGenerator
{
    Task<TeamPortraitResult> GenerateAsync(TeamPortraitRequest request, CancellationToken cancellationToken = default);
}

public sealed class TeamPortraitRequest
{
    public required TeamId TeamId { get; init; }
    public required string TeamName { get; init; }
    public string? OwnerName { get; init; }
    public string? PortraitNotes { get; init; }
    public required bool IsUserTeam { get; init; }
    public bool NormalImage { get; init; }
    public string? ProviderKey { get; init; }
    public int Spin { get; init; }
    public string? ArtStyleKey { get; init; }
    public string? CustomPrompt { get; init; }
    public TeamPortraitTone Tone => TeamPortraitPrompt.ToneFor(IsUserTeam, NormalImage);
}

public sealed class TeamPortraitResult
{
    public required bool Succeeded { get; init; }
    public string? Error { get; init; }
    public string? Provider { get; init; }
    public string? Model { get; init; }
}
