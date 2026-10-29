using System.Globalization;
using Evakuator.Domain.Entities;
using Evakuator.Norms.Models;
using Evakuator.Validation.Abstractions;

namespace Evakuator.Validation.Rules;

public sealed class R02_UniformSignScaleRule : INormRule
{
    public string Id => "R02";
    public IEnumerable<ValidationIssue> Evaluate(EvacuationPlan plan, NormDatabaseDto database)
    {
        var known = plan.Signs.Where(s => database.FindSign(s.Code) is not null && double.IsFinite(s.HeightMmOnPaper)).ToArray();
        if (known.Length < 2) yield break;
        // Compare the whole range, not only deviations from the configured default.
        var minimum = known.Min(s => s.HeightMmOnPaper);
        var maximum = known.Max(s => s.HeightMmOnPaper);
        if (maximum - minimum <= database.Parameters.UniformToleranceMm) yield break;
        foreach (var sign in known.Where(s => s.HeightMmOnPaper - minimum > database.Parameters.UniformToleranceMm))
            yield return new(Id, sign.Id, IssueSeverity.Error,
                $"Разный модуль: {sign.Code} — {sign.HeightMmOnPaper.ToString("0.##", CultureInfo.InvariantCulture)} мм; знаки плана должны иметь один модуль.", true);
    }
}
