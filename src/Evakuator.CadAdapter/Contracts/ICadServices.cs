using System.Collections.Immutable;
using Evakuator.Domain.Entities;
using Evakuator.Domain.Geometry;
using Evakuator.Norms.Models;
using Evakuator.Validation.Abstractions;

namespace Evakuator.CadAdapter.Contracts;

public interface ICadEntityRenderer
{
    ImmutableArray<VectorPrimitive> RenderSign(EvacuationSign sign, NormDatabaseDto database);
    ImmutableArray<VectorPrimitive> RenderPath(EvacuationPath path, NormDatabaseDto database);
}

public interface ICadHighlightService
{
    IReadOnlySet<string> HighlightedIds { get; }
    void SetIssues(IEnumerable<ValidationIssue> issues);
    void Clear();
}
