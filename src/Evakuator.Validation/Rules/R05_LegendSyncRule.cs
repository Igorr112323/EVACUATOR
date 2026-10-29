using Evakuator.Domain.Entities;
using Evakuator.Geometry;
using Evakuator.Norms.Models;
using Evakuator.Validation.Abstractions;

namespace Evakuator.Validation.Rules;

public sealed class R05_LegendSyncRule : INormRule
{
    public string Id => "R05";
    public IEnumerable<ValidationIssue> Evaluate(EvacuationPlan plan, NormDatabaseDto database)
    {
        var expected = new LegendLayoutEngine().Build(plan, database).Entries;
        if (plan.Legend is null)
        {
            yield return new(Id, plan.Id, IssueSeverity.Error, "Таблица условных обозначений отсутствует.", true);
            yield break;
        }
        var actual = plan.Legend.Entries;
        if (actual.Length != expected.Length ||
            !actual.OrderBy(e => e.Key, StringComparer.Ordinal).SequenceEqual(expected.OrderBy(e => e.Key, StringComparer.Ordinal)))
            yield return new(Id, plan.Id, IssueSeverity.Error,
                "Легенда не совпадает с фактическими знаками, типами путей, маркерами или их описаниями.", true);
    }
}
