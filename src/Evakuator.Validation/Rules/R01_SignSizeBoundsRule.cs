using System.Globalization;
using Evakuator.Domain.Entities;
using Evakuator.Norms.Models;
using Evakuator.Validation.Abstractions;

namespace Evakuator.Validation.Rules;

public sealed class R01_SignSizeBoundsRule : INormRule
{
    public string Id => "R01";
    public IEnumerable<ValidationIssue> Evaluate(EvacuationPlan plan, NormDatabaseDto database)
    {
        foreach (var sign in plan.Signs)
        {
            var definition = database.FindSign(sign.Code);
            if (definition is null)
            {
                yield return new(Id, sign.Id, IssueSeverity.Error, $"Неизвестный код знака {sign.Code}; проверьте каталог.", false);
                continue;
            }
            var height = sign.HeightMmOnPaper;
            if (!double.IsFinite(height) || height < definition.MinHeightMm || height > definition.MaxHeightMm)
                yield return new(Id, sign.Id, IssueSeverity.Error,
                    $"{sign.Code}: {height.ToString("0.##", CultureInfo.InvariantCulture)} мм на листе; диапазон {definition.MinHeightMm.ToString("0.##", CultureInfo.InvariantCulture)}–{definition.MaxHeightMm.ToString("0.##", CultureInfo.InvariantCulture)} мм.", true);
        }
    }
}
