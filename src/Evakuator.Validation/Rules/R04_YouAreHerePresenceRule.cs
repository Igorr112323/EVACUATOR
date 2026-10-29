using Evakuator.Domain.Entities;
using Evakuator.Norms.Models;
using Evakuator.Validation.Abstractions;

namespace Evakuator.Validation.Rules;

public sealed class R04_YouAreHerePresenceRule : INormRule
{
    public string Id => "R04";
    public IEnumerable<ValidationIssue> Evaluate(EvacuationPlan plan, NormDatabaseDto database)
    {
        if (plan.Markers.IsEmpty) yield return new(Id, plan.Id, IssueSeverity.Error, "Отсутствует точка «Вы находитесь здесь».", true);
        foreach (var marker in plan.Markers.Where(m => m.NeedsReview))
            yield return new(Id, marker.Id, IssueSeverity.Warning,
                string.IsNullOrWhiteSpace(marker.Note) ? "Проверьте точное положение точки «Вы находитесь здесь»." : marker.Note,
                RequiresHumanReview: true);
    }
}
