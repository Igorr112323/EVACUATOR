using Evakuator.Domain.Entities;
using Evakuator.Domain.ValueObjects;
using Evakuator.Geometry;
using Evakuator.Norms.Models;
using Evakuator.Validation.Abstractions;

namespace Evakuator.Validation.Rules;

public sealed class R06_SignCollisionAndOverlapRule : INormRule
{
    public string Id => "R06";
    public IEnumerable<ValidationIssue> Evaluate(EvacuationPlan plan, NormDatabaseDto database)
    {
        var generator = new SignGeometryGenerator();
        var bounds = new BoundingBox2D(0, 0, plan.Width, plan.Height);
        for (var i = 0; i < plan.Signs.Length; i++)
        {
            var sign = plan.Signs[i];
            if (sign.NeedsReview)
                yield return new(Id, sign.Id, IssueSeverity.Warning,
                    string.IsNullOrWhiteSpace(sign.ReviewNote) ? "Проверьте положение знака на плане." : sign.ReviewNote, RequiresHumanReview: true);
            if (sign.ModelHeight <= 0 || database.FindSign(sign.Code) is null) continue;
            var a = generator.Bounds(sign, database);
            if (!bounds.Contains(a))
                yield return new(Id, sign.Id, IssueSeverity.Error, $"{sign.Code}: знак выходит за границы плана.", true);
            for (var j = i + 1; j < plan.Signs.Length; j++)
            {
                var other = plan.Signs[j];
                if (other.ModelHeight <= 0 || database.FindSign(other.Code) is null) continue;
                var duplicate = sign.Code == other.Code && sign.InsertionPoint.DistanceTo(other.InsertionPoint) <= database.Parameters.DuplicateToleranceMm;
                var overlap = a.OverlapOfSmaller(generator.Bounds(other, database));
                if (duplicate || overlap > database.Parameters.OverlapThreshold)
                    yield return new(Id, sign.Id, IssueSeverity.Error,
                        duplicate ? $"Дублирующиеся знаки {sign.Code} в одной точке." : $"Знаки {sign.Code} и {other.Code} перекрываются на {overlap:P0} меньшей площади.",
                        true, other.Id);
            }
        }
    }
}
