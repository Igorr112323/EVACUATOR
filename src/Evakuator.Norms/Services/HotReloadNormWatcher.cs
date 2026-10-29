using Evakuator.Norms.Exceptions;
using Evakuator.Norms.Models;

namespace Evakuator.Norms.Services;

/// <summary>Publishes validated immutable snapshots only. A broken edit leaves the previous snapshot in use.</summary>
public sealed class HotReloadNormWatcher : IDisposable
{
    private readonly JsonNormRepository repository;
    private readonly FileSystemWatcher watcher;
    private readonly Timer timer;
    private readonly object gate = new();
    private NormDatabaseDto current;
    private bool disposed;

    public NormDatabaseDto Current => Volatile.Read(ref current);
    public event Action<NormDatabaseDto>? Reloaded;
    public event Action<Exception>? ReloadFailed;

    public HotReloadNormWatcher(JsonNormRepository repository)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        current = repository.Load();
        timer = new Timer(_ => ReloadNow(), null, Timeout.Infinite, Timeout.Infinite);
        watcher = new FileSystemWatcher(System.IO.Path.GetDirectoryName(repository.Path)!, System.IO.Path.GetFileName(repository.Path))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
            EnableRaisingEvents = true
        };
        watcher.Changed += OnChange;
        watcher.Created += OnChange;
        watcher.Renamed += OnChange;
    }

    private void OnChange(object sender, FileSystemEventArgs e)
    {
        lock (gate) { if (!disposed) timer.Change(250, Timeout.Infinite); }
    }

    public bool ReloadNow()
    {
        lock (gate)
        {
            if (disposed) return false;
            NormDatabaseDto next;
            try { next = repository.Load(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NormDatabaseValidationException)
            {
                NotifyFailure(ex);
                return false;
            }
            Volatile.Write(ref current, next);
            // Subscribers are isolated: an exception in a UI listener must not crash a timer thread.
            foreach (var subscriber in Reloaded?.GetInvocationList().Cast<Action<NormDatabaseDto>>() ?? [])
                try { subscriber(next); } catch (Exception ex) { NotifyFailure(ex); }
            return true;
        }
    }

    private void NotifyFailure(Exception exception)
    {
        foreach (var subscriber in ReloadFailed?.GetInvocationList().Cast<Action<Exception>>() ?? [])
            try { subscriber(exception); } catch { /* Event sinks are external to the watcher. */ }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            watcher.Dispose();
            timer.Dispose();
        }
    }
}
