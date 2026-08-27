using FantasyDraftAssistant.Data;
using FantasyDraftAssistant.Data.Database;
using FantasyDraftAssistant.Data.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FantasyDraftAssistant.Data.Tests;

public class DataLockEvaluatorTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Missing_lock_is_free()
    {
        var result = Inspect(existing: null, corrupt: false);
        Assert.Equal(DataLockKind.Free, result.Kind);
        Assert.False(result.RequiresWarning);
    }

    [Fact]
    public void Fresh_lock_on_another_host_is_live()
    {
        var result = Inspect(Holder("other-pc", pid: 9, age: TimeSpan.FromSeconds(20)));
        Assert.Equal(DataLockKind.Live, result.Kind);
        Assert.True(result.RequiresWarning);
    }

    [Fact]
    public void Old_lock_on_another_host_is_stale()
    {
        var result = Inspect(Holder("other-pc", pid: 9, age: TimeSpan.FromMinutes(6)));
        Assert.Equal(DataLockKind.Stale, result.Kind);
        Assert.True(result.RequiresWarning);
    }

    [Fact]
    public void Same_host_dead_process_does_not_warn()
    {
        var result = Inspect(Holder("this-pc", pid: 50, age: TimeSpan.FromSeconds(5)), alive: _ => false);
        Assert.Equal(DataLockKind.SameHostDead, result.Kind);
        Assert.False(result.RequiresWarning);
    }

    [Fact]
    public void Same_host_live_process_warns()
    {
        var result = Inspect(Holder("this-pc", pid: 50, age: TimeSpan.FromSeconds(5)), selfPid: 99, alive: pid => pid == 50);
        Assert.Equal(DataLockKind.Live, result.Kind);
        Assert.True(result.RequiresWarning);
    }

    [Fact]
    public void Same_host_same_pid_is_treated_as_leftover()
    {
        var result = Inspect(Holder("this-pc", pid: 42, age: TimeSpan.FromSeconds(5)), selfPid: 42, alive: _ => true);
        Assert.Equal(DataLockKind.SameHostDead, result.Kind);
        Assert.False(result.RequiresWarning);
    }

    [Fact]
    public void Our_session_is_ours()
    {
        var holder = Holder("this-pc", pid: 42, age: TimeSpan.Zero);
        var result = DataLockEvaluator.Inspect(holder, false, "this-pc", 42, holder.SessionId, T0, _ => true);
        Assert.Equal(DataLockKind.Ours, result.Kind);
        Assert.False(result.RequiresWarning);
    }

    [Fact]
    public void Corrupt_lock_warns()
    {
        var result = Inspect(existing: null, corrupt: true);
        Assert.Equal(DataLockKind.Corrupt, result.Kind);
        Assert.True(result.RequiresWarning);
    }

    [Fact]
    public void Live_dialog_names_the_other_computer()
    {
        var inspection = Inspect(Holder("kitchen-pc", pid: 7, age: TimeSpan.FromSeconds(12), user: "kajota"));
        var dialog = DataLockEvaluator.Dialog(inspection, "/data/fantasy");
        Assert.Equal("Database already open", dialog.Title);
        Assert.Contains("kitchen-pc", dialog.Body, StringComparison.Ordinal);
        Assert.Contains("kajota", dialog.Body, StringComparison.Ordinal);
        Assert.Contains("/data/fantasy", dialog.Detail, StringComparison.Ordinal);
    }

    private static DataLockInspection Inspect(
        DataLockHolder? existing = null,
        bool corrupt = false,
        int selfPid = 42,
        Func<int, bool>? alive = null) =>
        DataLockEvaluator.Inspect(
            existing,
            corrupt,
            "this-pc",
            selfPid,
            selfSessionId: null,
            T0,
            alive ?? (_ => false));

    private static DataLockHolder Holder(string host, int pid, TimeSpan age, string user = "pat") =>
        new("session-1", host, user, pid, T0 - age - TimeSpan.FromMinutes(1), T0 - age);
}

public class DataLockServiceTests : IDisposable
{
    private readonly string _root;
    private readonly List<DataLockService> _locks = [];

    public DataLockServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "fda-lock-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        foreach (var item in _locks)
            item.Dispose();
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void Acquire_writes_lock_and_inspect_is_ours()
    {
        var clock = new ManualClock(new DateTimeOffset(2026, 8, 25, 15, 0, 0, TimeSpan.Zero));
        var service = Create("pc-a", 11, clock);
        Assert.Equal(DataLockKind.Free, service.Inspect().Kind);

        var held = service.Acquire();
        Assert.True(File.Exists(service.LockPath));
        Assert.Equal(held.SessionId, service.Current?.SessionId);
        Assert.Equal(DataLockKind.Ours, service.Inspect().Kind);
    }

    [Fact]
    public void Second_computer_sees_a_live_lock()
    {
        var clock = new ManualClock(new DateTimeOffset(2026, 8, 25, 15, 0, 0, TimeSpan.Zero));
        var first = Create("pc-a", 11, clock);
        first.Acquire();

        var second = Create("pc-b", 22, clock);
        var inspection = second.Inspect();
        Assert.Equal(DataLockKind.Live, inspection.Kind);
        Assert.Equal("pc-a", inspection.Holder?.Host);
    }

    [Fact]
    public void Release_deletes_our_lock()
    {
        var clock = new ManualClock(new DateTimeOffset(2026, 8, 25, 15, 0, 0, TimeSpan.Zero));
        var service = Create("pc-a", 11, clock);
        service.Acquire();
        service.Release();
        Assert.False(File.Exists(service.LockPath));
        Assert.Null(service.Current);
    }

    [Fact]
    public void Heartbeat_detects_when_another_session_steals_the_lock()
    {
        var clock = new ManualClock(new DateTimeOffset(2026, 8, 25, 15, 0, 0, TimeSpan.Zero));
        var first = Create("pc-a", 11, clock);
        var stolen = 0;
        first.Stolen += (_, _) => stolen++;
        first.Acquire();

        var second = Create("pc-b", 22, clock);
        second.Acquire();
        first.Heartbeat();

        Assert.True(first.WasStolen);
        Assert.Equal(1, stolen);
        Assert.Contains(second.Current!.SessionId, File.ReadAllText(first.LockPath), StringComparison.Ordinal);
    }

    [Fact]
    public void Release_leaves_a_stolen_lock_in_place()
    {
        var clock = new ManualClock(new DateTimeOffset(2026, 8, 25, 15, 0, 0, TimeSpan.Zero));
        var first = Create("pc-a", 11, clock);
        first.Acquire();
        var second = Create("pc-b", 22, clock);
        var stolen = second.Acquire();
        first.Release();
        Assert.True(File.Exists(first.LockPath));
        Assert.Contains(stolen.SessionId, File.ReadAllText(first.LockPath), StringComparison.Ordinal);
    }

    [Fact]
    public void Unreadable_lock_file_is_corrupt()
    {
        var clock = new ManualClock(new DateTimeOffset(2026, 8, 25, 15, 0, 0, TimeSpan.Zero));
        var service = Create("pc-a", 11, clock);
        File.WriteAllText(service.LockPath, "{not-json");
        var inspection = service.Inspect();
        Assert.Equal(DataLockKind.Corrupt, inspection.Kind);
        Assert.True(inspection.RequiresWarning);
    }

    [Fact]
    public void AddFantasyDraftData_registers_the_lock_service()
    {
        var collection = new ServiceCollection();
        collection.AddFantasyDraftData(_root);
        using var services = collection.BuildServiceProvider();
        var lockService = services.GetRequiredService<DataLockService>();
        Assert.Equal(Path.GetFullPath(Path.Combine(_root, AppPaths.LockFileName)), lockService.LockPath);
        Assert.Equal(DataLockKind.Free, lockService.Inspect().Kind);
    }

    private DataLockService Create(string host, int pid, TimeProvider clock)
    {
        var service = new DataLockService(new AppPaths(_root), clock, _ => false, host, "tester", pid);
        _locks.Add(service);
        return service;
    }

    private sealed class ManualClock(DateTimeOffset start) : TimeProvider
    {
        public DateTimeOffset UtcNowValue { get; set; } = start;
        public override DateTimeOffset GetUtcNow() => UtcNowValue;
    }
}
