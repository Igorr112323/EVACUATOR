using System.Collections.Immutable;
using Evakuator.Domain.Entities;
using Evakuator.Domain.Enums;
using Evakuator.Norms.Models;

namespace Evakuator.Geometry;

public sealed class LegendLayoutEngine
{
    public LegendTable Build(EvacuationPlan plan, NormDatabaseDto database)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(database);
        var entries = ImmutableArray.CreateBuilder<LegendEntry>();
        foreach (var code in plan.Signs.Select(s => s.Code).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
            entries.Add(new LegendEntry(code, database.FindSign(code)?.OfficialDescription ?? $"Неизвестный знак: {code}"));
        if (plan.Paths.Any(p => p.Type == EvacuationPathType.Primary))
            entries.Add(new LegendEntry("PATH:Primary", "Основной путь эвакуации"));
        if (plan.Paths.Any(p => p.Type == EvacuationPathType.Secondary))
            entries.Add(new LegendEntry("PATH:Secondary", "Запасной путь эвакуации"));
        if (!plan.Markers.IsEmpty) entries.Add(new LegendEntry("HERE", "Вы находитесь здесь"));
        return new LegendTable { Entries = entries.ToImmutable() };
    }
}
