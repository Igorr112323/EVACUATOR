using System.Collections.Concurrent;
using Evakuator.CadAdapter.Contracts;
using Evakuator.Domain.Entities;
using Evakuator.Domain.Serialization;

namespace Evakuator.CadAdapter.Simulation;

/// <summary>Atomic immutable snapshots, bounded undo, and optimistic concurrency for tests and demo.</summary>
public sealed class InMemoryCadDocumentContext : ICadDocumentContext
{
    private const int MaximumUndo = 100;
    private readonly object gate = new();
    private readonly List<EvacuationPlan> history = [];
    private readonly ConcurrentQueue<Exception> observerErrors = new();
    private EvacuationPlan plan;
    private long revision;
    public event Action<CadSnapshot>? Changed;
    public IReadOnlyCollection<Exception> ObserverErrors => observerErrors.ToArray();

    public InMemoryCadDocumentContext(EvacuationPlan initial)
    {
        PlanJson.CheckStructure(initial);
        plan = initial;
    }

    public CadSnapshot Read() { lock (gate) return new(plan, revision); }

    public bool TryCommit(long expectedRevision, EvacuationPlan updated, string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        PlanJson.CheckStructure(updated);
        CadSnapshot snapshot;
        lock (gate)
        {
            if (expectedRevision != revision) return false;
            if (updated.Id != plan.Id) throw new InvalidOperationException("A transaction cannot replace a different document.");
            if (updated == plan) return true;
            history.Add(plan);
            if (history.Count > MaximumUndo) history.RemoveAt(0);
            plan = updated;
            revision++;
            snapshot = new(plan, revision);
        }
        Notify(snapshot);
        return true;
    }

    public bool Undo()
    {
        CadSnapshot snapshot;
        lock (gate)
        {
            if (history.Count == 0) return false;
            plan = history[^1];
            history.RemoveAt(history.Count - 1);
            revision++;
            snapshot = new(plan, revision);
        }
        Notify(snapshot);
        return true;
    }

    private void Notify(CadSnapshot snapshot)
    {
        // A failing observer must not turn a committed transaction into an apparent failure.
        foreach (var observer in Changed?.GetInvocationList().Cast<Action<CadSnapshot>>() ?? [])
            try { observer(snapshot); } catch (Exception ex) { observerErrors.Enqueue(ex); }
    }
}
