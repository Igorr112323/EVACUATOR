using System.Collections.Immutable;
using Evakuator.Domain.Entities;
using Evakuator.Domain.Geometry;
using Evakuator.Domain.ValueObjects;
using Evakuator.Norms.Models;

namespace Evakuator.Geometry;

/// <summary>Expands a normalized recipe into rotated model-space vectors, never raster images.</summary>
public sealed class SignGeometryGenerator
{
    public ParametricRecipe Generate(EvacuationSign sign, NormDatabaseDto database)
    {
        ArgumentNullException.ThrowIfNull(sign);
        ArgumentNullException.ThrowIfNull(database);
        var def = database.FindSign(sign.Code) ?? throw new ArgumentException($"Unknown sign: {sign.Code}", nameof(sign));
        var h = sign.ModelHeight;
        if (!double.IsFinite(h) || h <= 0 || !double.IsFinite(sign.RotationDegrees))
            throw new ArgumentOutOfRangeException(nameof(sign), "Sign geometry requires positive finite model height and rotation.");
        var w = h * def.AspectRatio;
        Point2D P(double x, double y) => new Point2D(sign.InsertionPoint.X + x * w, sign.InsertionPoint.Y + y * h)
            .RotateAround(sign.InsertionPoint, sign.RotationDegrees);
        ImmutableArray<Point2D> Points(params (double X, double Y)[] values) => values.Select(p => P(p.X, p.Y)).ToImmutableArray();
        var result = ImmutableArray.CreateBuilder<VectorPrimitive>();
        switch (def.Shape)
        {
            case SignShape.Square:
            case SignShape.Rectangle2x1:
                result.Add(new PolygonPrimitive(Points((0,0),(1,0),(1,1),(0,1)), def.BackgroundColor));
                result.Add(new PolylinePrimitive(Points((.045,.045),(.955,.045),(.955,.955),(.045,.955),(.045,.045)), "#FFFFFF", h * .035));
                break;
            case SignShape.CircleSlash:
                result.Add(new CirclePrimitive(P(.5,.5), h * .47, def.BackgroundColor, "#D00000", h * .065));
                break;
            case SignShape.EquilateralTriangle:
                // An equilateral triangle centred inside a square carrier.
                result.Add(new PolygonPrimitive(Points((0,.067), (1,.067), (.5,.933)), "#171717"));
                result.Add(new PolygonPrimitive(Points((.09,.12), (.91,.12), (.5,.83)), def.BackgroundColor));
                break;
            default:
                throw new InvalidOperationException($"Unsupported carrier {def.Shape}.");
        }

        foreach (var primitive in def.Primitives)
        {
            var sw = primitive.StrokeWidth * h;
            switch (primitive.Type)
            {
                case "Rect":
                    result.Add(new PolygonPrimitive(Points((primitive.X, primitive.Y),
                        (primitive.X + primitive.Width, primitive.Y), (primitive.X + primitive.Width, primitive.Y + primitive.Height),
                        (primitive.X, primitive.Y + primitive.Height)), primitive.Fill, primitive.Stroke, sw));
                    break;
                case "Circle":
                    result.Add(new CirclePrimitive(P(primitive.X, primitive.Y), primitive.Radius * h, primitive.Fill, primitive.Stroke, sw));
                    break;
                case "Arc":
                    result.Add(new ArcPrimitive(P(primitive.X, primitive.Y), primitive.Radius * h,
                        primitive.StartAngleDegrees + sign.RotationDegrees, primitive.SweepAngleDegrees, primitive.Stroke, sw));
                    break;
                case "Polygon":
                    result.Add(new PolygonPrimitive(primitive.Points.Select(point => P(point.X, point.Y)).ToImmutableArray(), primitive.Fill, primitive.Stroke, sw));
                    break;
                case "Polyline":
                    result.Add(new PolylinePrimitive(primitive.Points.Select(point => P(point.X, point.Y)).ToImmutableArray(), primitive.Stroke, sw));
                    break;
                case "Text":
                    result.Add(new TextPrimitive(P(primitive.X, primitive.Y), primitive.Text, primitive.FontSize * h, primitive.Fill, sign.RotationDegrees));
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported vector primitive {primitive.Type}.");
            }
        }
        if (def.Shape == SignShape.CircleSlash)
            result.Add(new PolylinePrimitive(Points((.17,.83),(.83,.17)), "#D00000", h * .07));
        return new ParametricRecipe(result.ToImmutable(), BoundingBox2D.FromPoints(Points((0,0),(1,0),(1,1),(0,1))));
    }

    public BoundingBox2D Bounds(EvacuationSign sign, NormDatabaseDto database) => Generate(sign, database).Bounds;
}
