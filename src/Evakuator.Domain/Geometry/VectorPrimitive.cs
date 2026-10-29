using System.Collections.Immutable;
using Evakuator.Domain.ValueObjects;

namespace Evakuator.Domain.Geometry;

/// <summary>Absolute model-space vector geometry. A polygon is closed; a polyline is not.</summary>
public abstract record VectorPrimitive(string Fill, string Stroke, double StrokeWidth);
public sealed record PolygonPrimitive(ImmutableArray<Point2D> Points, string Fill, string Stroke = "none", double StrokeWidth = 0)
    : VectorPrimitive(Fill, Stroke, StrokeWidth);
public sealed record PolylinePrimitive(ImmutableArray<Point2D> Points, string Stroke, double StrokeWidth, bool Dashed = false)
    : VectorPrimitive("none", Stroke, StrokeWidth);
public sealed record CirclePrimitive(Point2D Center, double Radius, string Fill, string Stroke = "none", double StrokeWidth = 0)
    : VectorPrimitive(Fill, Stroke, StrokeWidth);
/// <summary>Circular open arc in model space: degrees are counter-clockwise in the Y-up coordinate system.</summary>
public sealed record ArcPrimitive(Point2D Center, double Radius, double StartAngleDegrees, double SweepAngleDegrees, string Stroke, double StrokeWidth)
    : VectorPrimitive("none", Stroke, StrokeWidth)
{
    public Point2D At(double fraction)
    {
        var angle = (StartAngleDegrees + SweepAngleDegrees * fraction) * Math.PI / 180;
        return Center.Translate(Radius * Math.Cos(angle), Radius * Math.Sin(angle));
    }
}
public sealed record TextPrimitive(Point2D Position, string Text, double FontSize, string Fill, double RotationDegrees = 0)
    : VectorPrimitive(Fill, "none", 0);

public sealed record ParametricRecipe(ImmutableArray<VectorPrimitive> Primitives, BoundingBox2D Bounds);
