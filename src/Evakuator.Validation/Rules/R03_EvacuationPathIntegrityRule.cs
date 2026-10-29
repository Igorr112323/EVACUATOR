using Evakuator.Domain.Entities;
using Evakuator.Domain.Enums;
using Evakuator.Geometry;
using Evakuator.Norms.Models;
using Evakuator.Validation.Abstractions;

namespace Evakuator.Validation.Rules;

public sealed class R03_EvacuationPathIntegrityRule : INormRule
{
    public string Id => "R03";
    public IEnumerable<ValidationIssue> Evaluate(EvacuationPlan plan, NormDatabaseDto database)
    {
        if (plan.Paths.IsEmpty)
            yield return new(Id, plan.Id, IssueSeverity.Error, "Не задан ни один путь эвакуации; требуется проектирование маршрута.");
        foreach (var path in plan.Paths)
        {
            var problems = new List<string>();
            if (!path.HasDirectionArrow) problems.Add("нет стрелки направления");
            if (path.Points.Length < 2) problems.Add("менее двух вершин");
            if (PolylineMath.HasDegenerateSegments(path.Points)) problems.Add("вырожденный сегмент");
            var cleaned = PolylineMath.RemoveConsecutiveDuplicates(path.Points);
            var selfIntersection = PolylineMath.HasSelfIntersection(cleaned);
            if (selfIntersection) problems.Add("самопересечение");
            var outside = path.Points.Any(p => p.X < 0 || p.Y < 0 || p.X > plan.Width || p.Y > plan.Height);
            if (outside) problems.Add("вершины находятся вне границ плана");
            var expectedStyle = path.Type == EvacuationPathType.Primary ? PathLineStyle.Solid : PathLineStyle.Dashed;
            if (path.LineStyle != expectedStyle) problems.Add("неверный тип линии");
            if (!string.Equals(path.Color, database.Parameters.PathColor, StringComparison.OrdinalIgnoreCase)) problems.Add("неверный цвет");
            if (Math.Abs(path.LineWeightMmOnPaper - database.Parameters.PathLineWeightMm) > 1e-6) problems.Add("неверная толщина линии");
            var exits = plan.Signs.Where(s => database.FindSign(s.Code)?.IsExit == true).ToArray();
            var distance = path.Points.IsEmpty || exits.Length == 0 ? double.PositiveInfinity : exits.Min(s => s.InsertionPoint.DistanceTo(path.Points[^1]));
            if (distance > database.Parameters.SnapToleranceMm)
                problems.Add("конец далеко от знака выхода или выход отсутствует");
            if (problems.Count > 0)
            {
                var canFix = cleaned.Length >= 2 && !selfIntersection && !outside && distance <= database.Parameters.MaxAutoSnapDistanceMm;
                yield return new(Id, path.Id, IssueSeverity.Error, string.Join("; ", problems) + ".", canFix);
            }
            if (path.NeedsReview)
                yield return new(Id, path.Id, IssueSeverity.Warning,
                    string.IsNullOrWhiteSpace(path.ReviewNote) ? "Маршрут требует проверки проектировщиком." : path.ReviewNote,
                    RequiresHumanReview: true);
        }
    }
}
