using System.Collections.Immutable;
using Evakuator.CadAdapter.Contracts;
using Evakuator.Domain.Entities;
using Evakuator.Domain.Geometry;
using Evakuator.Geometry;
using Evakuator.Norms.Models;
using Evakuator.Validation.Abstractions;

namespace Evakuator.CadAdapter.Simulation;

public sealed class InMemoryCadEntityRenderer : ICadEntityRenderer
{
    public ImmutableArray<VectorPrimitive> RenderSign(EvacuationSign sign, NormDatabaseDto database) => new SignGeometryGenerator().Generate(sign, database).Primitives;
    public ImmutableArray<VectorPrimitive> RenderPath(EvacuationPath path, NormDatabaseDto database) => new PathGeometryGenerator().Generate(path, database.Parameters);
}

public sealed class InMemoryCadHighlightService : ICadHighlightService
{
    private ImmutableHashSet<string> ids = ImmutableHashSet<string>.Empty.WithComparer(StringComparer.Ordinal);
    public IReadOnlySet<string> HighlightedIds => ids;
    public void SetIssues(IEnumerable<ValidationIssue> issues) => ids = issues
        .Where(i => i.Severity == IssueSeverity.Error)
        .SelectMany(i => i.RelatedObjectId is null ? new[] { i.ObjectId } : new[] { i.ObjectId, i.RelatedObjectId })
        .ToImmutableHashSet(StringComparer.Ordinal);
    public void Clear() => ids = ImmutableHashSet<string>.Empty.WithComparer(StringComparer.Ordinal);
}
