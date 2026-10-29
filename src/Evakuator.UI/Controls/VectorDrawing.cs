using System.Collections.Immutable;
using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Evakuator.Domain.Geometry;
using Evakuator.Domain.ValueObjects;

namespace Evakuator.UI.Controls;

internal static class VectorDrawing
{
    public static void Draw(DrawingContext context, VectorPrimitive primitive, Func<Point2D, Point> map, double scale)
    {
        var brush = Brush(primitive.Fill);
        IPen? pen = primitive.Stroke == "none" ? null : new Pen(Brush(primitive.Stroke), Math.Max(.6, primitive.StrokeWidth * scale),
            primitive is PolylinePrimitive { Dashed: true } ? new DashStyle([4d, 3d], 0) : null,
            PenLineCap.Round, PenLineJoin.Round);
        switch (primitive)
        {
            case PolygonPrimitive polygon:
                DrawPoints(context, polygon.Points, map, brush, pen, true); break;
            case PolylinePrimitive line:
                DrawPoints(context, line.Points, map, null, pen, false); break;
            case CirclePrimitive circle:
                context.DrawEllipse(brush, pen, map(circle.Center), circle.Radius * scale, circle.Radius * scale); break;
            case ArcPrimitive arc:
                var segments = Math.Max(2, (int)Math.Ceiling(Math.Abs(arc.SweepAngleDegrees) / 8));
                DrawPoints(context, Enumerable.Range(0, segments + 1).Select(i => arc.At((double)i / segments)).ToImmutableArray(), map, null, pen, false); break;
            case TextPrimitive text:
                var point = map(text.Position);
                var size = Math.Max(4, text.FontSize * scale);
                var formatted = new FormattedText(text.Text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                    new Typeface("sans-serif"), size, brush ?? Brushes.Black);
                using (context.PushTransform(Matrix.CreateTranslation(-point.X, -point.Y) *
                    Matrix.CreateRotation(-text.RotationDegrees * Math.PI / 180) * Matrix.CreateTranslation(point.X, point.Y)))
                    context.DrawText(formatted, new Point(point.X, point.Y - size));
                break;
        }
    }

    private static void DrawPoints(DrawingContext context, ImmutableArray<Point2D> points, Func<Point2D, Point> map,
        IBrush? brush, IPen? pen, bool closed)
    {
        if (points.IsDefaultOrEmpty) return;
        var geometry = new StreamGeometry();
        using (var sink = geometry.Open())
        {
            sink.BeginFigure(map(points[0]), closed);
            for (var i = 1; i < points.Length; i++) sink.LineTo(map(points[i]));
            sink.EndFigure(closed);
        }
        context.DrawGeometry(brush, pen, geometry);
    }

    private static IBrush? Brush(string color)
    {
        if (color == "none") return null;
        try { return new SolidColorBrush(Color.Parse(color)); }
        catch (FormatException) { return Brushes.Red; }
    }
}
