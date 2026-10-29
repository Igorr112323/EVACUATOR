using Evakuator.Domain.Entities;

namespace Evakuator.CadAdapter.Contracts;

public sealed record CadSnapshot(EvacuationPlan Plan, long Revision);

/// <summary>Host-independent optimistic transaction boundary. Snapshots must be immutable.</summary>
public interface ICadDocumentContext
{
    CadSnapshot Read();
    bool TryCommit(long expectedRevision, EvacuationPlan updated, string description);
    bool Undo();
    event Action<CadSnapshot>? Changed;
}
