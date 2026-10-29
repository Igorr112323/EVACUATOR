using System.Collections.Immutable;
using Evakuator.Domain.Entities;
using Evakuator.Domain.Enums;
using Evakuator.Domain.Geometry;
using Evakuator.Domain.ValueObjects;
using Evakuator.Norms.Models;

namespace Evakuator.Geometry;

public sealed class PathGeometryGenerator
{
    private const int MaximumArrows = 10000;

    public ImmutableArray<VectorPrimitive> Generate(EvacuationPath path, NormParametersDto parameters)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(parameters);
        if (path.Points.Length < 2) return [];
        if (!double.IsFinite(path.DrawingScale) || path.DrawingScale <= 0) throw new ArgumentOutOfRangeException(nameof(path));
        var lineWidth = Math.Max(0.1, path.LineWeightMmOnPaper * path.DrawingScale);
        var output = ImmutableArray.CreateBuilder<VectorPrimitive>();
        output.Add(new PolylinePrimitive(path.Points, SafeColor(path.Color), lineWidth, path.LineStyle == PathLineStyle.Dashed));
        if (!path.HasDirectionArrow) return output.ToImmutable();
        if (!double.IsFinite(parameters.ArrowSpacingMm) || parameters.ArrowSpacingMm <= 0 ||
            !double.IsFinite(parameters.ArrowSizeMm) || parameters.ArrowSizeMm <= 0)
            throw new ArgumentOutOfRangeException(nameof(parameters));
        var segments = new List<(Point2D Start, Point2D End, double Length)>();
        for (var i = 1; i < path.Points.Length; i++)
        {
            var length = path.Points[i - 1].DistanceTo(path.Points[i]);
            if (length > 1e-9) segments.Add((path.Points[i - 1], path.Points[i], length));
        }
        var total = segments.Sum(s => s.Length);
        if (total <= 0) return output.ToImmutable();
        if (total / parameters.ArrowSpacingMm > MaximumArrows)
            throw new InvalidOperationException("Arrow count exceeds the safety limit.");
        var distances = new List<double>();
        for (var d = parameters.ArrowSpacingMm; d < total - 1e-7; d += parameters.ArrowSpacingMm)
            if (total - d >= Math.Max(parameters.ArrowSizeMm * 1.2, parameters.ArrowSpacingMm * .25)) distances.Add(d);
        distances.Add(total); // Always a final direction arrow.
        foreach (var distance in distances)
        {
            var remaining = distance;
            foreach (var segment in segments)
            {
                if (remaining > segment.Length + 1e-8) { remaining -= segment.Length; continue; }
                var direction = new Vector2D(segment.End.X - segment.Start.X, segment.End.Y - segment.Start.Y).Normalize();
                var tip = new Point2D(segment.Start.X + direction.X * remaining, segment.Start.Y + direction.Y * remaining);
                var size = Math.Min(parameters.ArrowSizeMm, segment.Length * .8);
                var back = tip.Translate(-direction.X * size, -direction.Y * size);
                var side = size * .40;
                output.Add(new PolygonPrimitive([tip, back.Translate(-direction.Y * side, direction.X * side),
                    back.Translate(direction.Y * side, -direction.X * side)], SafeColor(path.Color)));
                break;
            }
        }
        return output.ToImmutable();
    }

    private static string SafeColor(string color)
    {
        try { return RgbColor.Parse(color).ToString(); }
        catch (FormatException) { return "#D00000"; }
    }
}
