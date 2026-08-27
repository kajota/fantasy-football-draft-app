using System.Diagnostics;
using System.Text.Json;
using FantasyDraftAssistant.Core.Serialization;
using FantasyDraftAssistant.Data.Database;

namespace FantasyDraftAssistant.Data.Services;

public sealed class DataLockService : IDisposable
{
    public static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(30);

    private readonly AppPaths _paths;
    private readonly TimeProvider _clock;
    private readonly Func<int, bool> _isProcessAlive;
    private readonly string _host;
    private readonly string _user;
    private readonly int _pid;
    private readonly object _gate = new();
    private DataLockHolder? _ours;
    private Timer? _timer;
    private bool _stolen;

    public DataLockService(AppPaths paths)
        : this(paths, TimeProvider.System, ProcessIsAlive, Environment.MachineName, Environment.UserName, Environment.ProcessId)
    {
    }

    public DataLockService(
        AppPaths paths,
        TimeProvider clock,
        Func<int, bool> isProcessAlive,
        string host,
        string user,
        int pid)
    {
        _paths = paths;
        _clock = clock;
        _isProcessAlive = isProcessAlive;
        _host = host;
        _user = user;
        _pid = pid;
    }

    public string LockPath => _paths.LockFilePath;

    public DataLockHolder? Current
    {
        get
        {
            lock (_gate)
                return _ours;
        }
    }

    public bool WasStolen
    {
        get
        {
            lock (_gate)
                return _stolen;
        }
    }

    public event EventHandler? Stolen;

    public DataLockInspection Inspect()
    {
        var (holder, corrupt) = TryRead();
        lock (_gate)
        {
            return DataLockEvaluator.Inspect(
                holder,
                corrupt,
                _host,
                _pid,
                _ours?.SessionId,
                _clock.GetUtcNow(),
                _isProcessAlive);
        }
    }

    public DataLockHolder Acquire()
    {
        DataLockHolder record;
        lock (_gate)
        {
            var now = _clock.GetUtcNow();
            record = new DataLockHolder(
                Guid.NewGuid().ToString("N"),
                _host,
                _user,
                _pid,
                now,
                now);
            Write(record);
            _ours = record;
            _stolen = false;
            StartTimerUnlocked();
        }

        return record;
    }

    public void Heartbeat()
    {
        EventHandler? stolen = null;
        lock (_gate)
        {
            if (_ours is null)
                return;

            var (onDisk, corrupt) = TryRead();
            if (!corrupt && onDisk is not null
                && !string.Equals(onDisk.SessionId, _ours.SessionId, StringComparison.Ordinal))
            {
                _stolen = true;
                StopTimerUnlocked();
                stolen = Stolen;
            }
            else
            {
                var updated = _ours with { HeartbeatAt = _clock.GetUtcNow() };
                Write(updated);
                _ours = updated;
            }
        }

        stolen?.Invoke(this, EventArgs.Empty);
    }

    public void Release()
    {
        lock (_gate)
        {
            StopTimerUnlocked();
            if (_ours is null)
                return;

            var (onDisk, corrupt) = TryRead();
            if (corrupt || onDisk is null
                || string.Equals(onDisk.SessionId, _ours.SessionId, StringComparison.Ordinal))
            {
                try
                {
                    if (File.Exists(LockPath))
                        File.Delete(LockPath);
                }
                catch (IOException)
                {
                    // Best effort. A leftover lock goes stale.
                }
            }

            _ours = null;
        }
    }

    public void Dispose() => Release();

    private void StartTimerUnlocked()
    {
        _timer ??= new Timer(
            _ =>
            {
                try
                {
                    Heartbeat();
                }
                catch (Exception ex) when (ex is IOException or JsonException)
                {
                    // Next tick retries. Never throw out of the timer.
                }
            },
            null,
            HeartbeatInterval,
            HeartbeatInterval);
    }

    private void StopTimerUnlocked()
    {
        _timer?.Dispose();
        _timer = null;
    }

    private (DataLockHolder? Holder, bool Corrupt) TryRead()
    {
        try
        {
            if (!File.Exists(LockPath))
                return (null, false);

            var json = File.ReadAllText(LockPath);
            if (string.IsNullOrWhiteSpace(json))
                return (null, true);

            var holder = JsonSerializer.Deserialize<DataLockHolder>(json, DraftJson.Options);
            if (holder is null
                || string.IsNullOrWhiteSpace(holder.SessionId)
                || string.IsNullOrWhiteSpace(holder.Host))
            {
                return (null, true);
            }

            return (holder, false);
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidOperationException)
        {
            return (null, true);
        }
    }

    private void Write(DataLockHolder holder)
    {
        Directory.CreateDirectory(_paths.Root);
        var json = DraftJson.Serialize(holder);
        var temp = LockPath + ".tmp";
        File.WriteAllText(temp, json);
        File.Move(temp, LockPath, overwrite: true);
    }

    internal static bool ProcessIsAlive(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return false;
        }
    }
}
