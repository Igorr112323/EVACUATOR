using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Xml;
using Evakuator.Domain.Entities;
using Evakuator.Domain.Enums;
using Evakuator.Domain.Geometry;
using Evakuator.Domain.ValueObjects;
using Evakuator.Norms.Models;

namespace Evakuator.Geometry;

/// <summary>Self-contained SVG; text and attributes are written through an XML writer.</summary>
public sealed class SvgPreviewExporter
{
    private const string SvgNamespace = "http://www.w3.org/2000/svg";
    private readonly SignGeometryGenerator signs = new();
    private readonly PathGeometryGenerator paths = new();
    private readonly LegendLayoutEngine legends = new();

    public string ExportSign(EvacuationSign sign, NormDatabaseDto database)
    {
        var recipe = signs.Generate(sign, database);
        var bounds = recipe.Bounds;
        var margin = Math.Max(bounds.Width, bounds.Height) * .08;
        return Document(bounds.MinX - margin, -(bounds.MaxY + margin), bounds.Width + 2 * margin, bounds.Height + 2 * margin, writer =>
        {
            Begin(writer, "g"); Attr(writer, "transform", "scale(1,-1)");
            foreach (var primitive in recipe.Primitives) Draw(writer, primitive, true);
            writer.WriteEndElement();
        });
    }

    public string ExportPlan(EvacuationPlan plan, NormDatabaseDto database, ISet<string>? highlightedObjectIds = null)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var margin = Math.Max(plan.Width, plan.Height) * .035;
        return Document(-margin, -plan.Height - margin, plan.Width + 2 * margin, plan.Height + 2 * margin, writer =>
        {
            Begin(writer, "rect"); Attr(writer, "x", "0"); Attr(writer, "y", F(-plan.Height));
            Attr(writer, "width", F(plan.Width)); Attr(writer, "height", F(plan.Height)); Attr(writer, "fill", "#FFFFFF"); writer.WriteEndElement();
            Begin(writer, "g"); Attr(writer, "transform", "scale(1,-1)");
            var boundary = new PolylinePrimitive([new(0,0), new(plan.Width,0), new(plan.Width,plan.Height), new(0,plan.Height), new(0,0)], "#334155", Math.Max(plan.Width, plan.Height) * .004);
            Draw(writer, boundary, true);
            foreach (var background in plan.Background.Where(x => x.Points.Length >= 2))
                Draw(writer, new PolylinePrimitive(background.Points, "#9AA8B2", Math.Max(plan.Width, plan.Height) * .002), true);
            foreach (var path in plan.Paths)
            {
                foreach (var primitive in paths.Generate(path, database.Parameters)) Draw(writer, primitive, true);
                if (highlightedObjectIds?.Contains(path.Id) == true && path.Points.Length >= 2)
                    Draw(writer, new PolylinePrimitive(path.Points, "#D00000", Math.Max(plan.Width, plan.Height) * .006, true), true);
            }
            foreach (var sign in plan.Signs)
            {
                if (database.FindSign(sign.Code) is null || sign.ModelHeight <= 0)
                {
                    Draw(writer, new TextPrimitive(sign.InsertionPoint, $"? {sign.Code}", Math.Max(plan.Width, plan.Height) * .025, "#D00000"), true);
                    continue;
                }
                var recipe = signs.Generate(sign, database);
                foreach (var primitive in recipe.Primitives) Draw(writer, primitive, true);
                if (highlightedObjectIds?.Contains(sign.Id) == true) DrawHighlight(writer, recipe.Bounds, plan.Width * .003);
            }
            foreach (var marker in plan.Markers)
            {
                var r = Math.Max(plan.Width, plan.Height) * .012;
                Draw(writer, new CirclePrimitive(marker.Position, r, "#165EA8", "#FFFFFF", r * .18), true);
                Draw(writer, new TextPrimitive(marker.Position.Translate(r * 1.4, r * 1.2), marker.NeedsReview ? "Вы здесь (?)" : "Вы здесь", r * 1.3, "#16324D"), true);
            }
            writer.WriteEndElement();
        });
    }

    public string ExportLegend(EvacuationPlan plan, NormDatabaseDto database)
    {
        var legend = legends.Build(plan, database);
        const double rowHeight = 72;
        var height = 80 + rowHeight * legend.Entries.Length;
        return Document(0, 0, 900, Math.Max(height, 100), writer =>
        {
            Begin(writer, "rect"); Attr(writer, "width", "900"); Attr(writer, "height", F(height)); Attr(writer, "fill", "#FFFFFF"); writer.WriteEndElement();
            SimpleText(writer, 25, 42, "УСЛОВНЫЕ ОБОЗНАЧЕНИЯ", 24, "#142838");
            for (var i = 0; i < legend.Entries.Length; i++)
            {
                var entry = legend.Entries[i];
                var top = 64 + i * rowHeight;
                var def = database.FindSign(entry.Key);
                if (def is not null)
                {
                    var sign = new EvacuationSign { Id = "legend", Code = entry.Key, InsertionPoint = new Point2D(25, -(top + 54)), ModelHeight = 48, DrawingScale = 1 };
                    Begin(writer, "g"); Attr(writer, "transform", "scale(1,-1)");
                    foreach (var primitive in signs.Generate(sign, database).Primitives) Draw(writer, primitive, true);
                    writer.WriteEndElement();
                }
                else if (entry.Key.StartsWith("PATH:", StringComparison.Ordinal))
                {
                    Begin(writer, "line"); Attr(writer, "x1", "25"); Attr(writer, "x2", "78"); Attr(writer, "y1", F(top + 30)); Attr(writer, "y2", F(top + 30));
                    Attr(writer, "stroke", database.Parameters.PathColor); Attr(writer, "stroke-width", "5");
                    if (entry.Key == "PATH:Secondary") Attr(writer, "stroke-dasharray", "8 6"); writer.WriteEndElement();
                    Begin(writer, "polygon"); Attr(writer, "points", $"78,{F(top + 30)} 65,{F(top + 23)} 65,{F(top + 37)}"); Attr(writer, "fill", database.Parameters.PathColor); writer.WriteEndElement();
                }
                else if (entry.Key == "HERE")
                {
                    Begin(writer, "circle"); Attr(writer, "cx", "49"); Attr(writer, "cy", F(top + 30)); Attr(writer, "r", "12"); Attr(writer, "fill", "#165EA8"); writer.WriteEndElement();
                }
                var displayKey = entry.Key.StartsWith("PATH:", StringComparison.Ordinal) ? "ЛИНИЯ" : entry.Key == "HERE" ? "МАРКЕР" : entry.Key;
                SimpleText(writer, 120, top + 37, displayKey, 19, "#142838");
                SimpleText(writer, 255, top + 37, entry.Description, 19, "#334155");
                Begin(writer, "line"); Attr(writer, "x1", "20"); Attr(writer, "x2", "880"); Attr(writer, "y1", F(top + rowHeight - 4)); Attr(writer, "y2", F(top + rowHeight - 4)); Attr(writer, "stroke", "#D7E2E7"); writer.WriteEndElement();
            }
        });
    }

    private static string Document(double x, double y, double width, double height, Action<XmlWriter> body)
    {
        var builder = new StringBuilder();
        using (var writer = XmlWriter.Create(builder, new XmlWriterSettings { OmitXmlDeclaration = true, Indent = true }))
        {
            writer.WriteStartElement("svg", SvgNamespace);
            Attr(writer, "viewBox", $"{F(x)} {F(y)} {F(width)} {F(height)}");
            Attr(writer, "role", "img");
            Begin(writer, "title"); writer.WriteString("Эвакуатор: учебное векторное превью, требует проверки проектировщиком"); writer.WriteEndElement();
            body(writer);
            writer.WriteEndElement();
        }
        return builder.ToString();
    }

    private static void Draw(XmlWriter writer, VectorPrimitive primitive, bool yUp)
    {
        switch (primitive)
        {
            case PolygonPrimitive polygon:
                Begin(writer, "polygon"); Attr(writer, "points", FormatPoints(polygon.Points)); Paint(writer, polygon); writer.WriteEndElement(); break;
            case PolylinePrimitive line:
                Begin(writer, "polyline"); Attr(writer, "points", FormatPoints(line.Points)); Paint(writer, line);
                Attr(writer, "stroke-linejoin", "round"); Attr(writer, "stroke-linecap", "round");
                if (line.Dashed) Attr(writer, "stroke-dasharray", $"{F(line.StrokeWidth * 6)} {F(line.StrokeWidth * 4)}");
                writer.WriteEndElement(); break;
            case CirclePrimitive circle:
                Begin(writer, "circle"); Attr(writer, "cx", F(circle.Center.X)); Attr(writer, "cy", F(circle.Center.Y)); Attr(writer, "r", F(circle.Radius)); Paint(writer, circle); writer.WriteEndElement(); break;
            case ArcPrimitive arc:
                var start = arc.At(0); var end = arc.At(1);
                Begin(writer, "path");
                Attr(writer, "d", $"M {F(start.X)} {F(start.Y)} A {F(arc.Radius)} {F(arc.Radius)} 0 {(Math.Abs(arc.SweepAngleDegrees) > 180 ? 1 : 0)} {(arc.SweepAngleDegrees > 0 ? 1 : 0)} {F(end.X)} {F(end.Y)}");
                Paint(writer, arc); Attr(writer, "stroke-linecap", "round"); writer.WriteEndElement(); break;
            case TextPrimitive text:
                Begin(writer, "text");
                Attr(writer, "transform", $"translate({F(text.Position.X)} {F(text.Position.Y)}) rotate({F(text.RotationDegrees)})" + (yUp ? " scale(1,-1)" : ""));
                Attr(writer, "font-family", "sans-serif"); Attr(writer, "font-weight", "bold"); Attr(writer, "font-size", F(text.FontSize)); Attr(writer, "fill", text.Fill);
                writer.WriteString(text.Text); writer.WriteEndElement(); break;
            default: throw new InvalidOperationException("Unsupported geometry type.");
        }
    }

    private static void DrawHighlight(XmlWriter writer, BoundingBox2D bounds, double width)
    {
        Draw(writer, new PolylinePrimitive([new(bounds.MinX,bounds.MinY),new(bounds.MaxX,bounds.MinY),
            new(bounds.MaxX,bounds.MaxY),new(bounds.MinX,bounds.MaxY),new(bounds.MinX,bounds.MinY)], "#D00000", width, true), true);
    }

    private static void Paint(XmlWriter writer, VectorPrimitive primitive)
    {
        Attr(writer, "fill", primitive.Fill); Attr(writer, "stroke", primitive.Stroke);
        if (primitive.StrokeWidth > 0) Attr(writer, "stroke-width", F(primitive.StrokeWidth));
    }
    private static void SimpleText(XmlWriter writer, double x, double y, string text, double size, string color)
    {
        Begin(writer, "text"); Attr(writer, "x", F(x)); Attr(writer, "y", F(y)); Attr(writer, "font-family", "sans-serif");
        Attr(writer, "font-size", F(size)); Attr(writer, "fill", color); writer.WriteString(text); writer.WriteEndElement();
    }
    private static string FormatPoints(ImmutableArray<Point2D> points) => string.Join(" ", points.Select(p => $"{F(p.X)},{F(p.Y)}"));
    private static string F(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    private static void Begin(XmlWriter writer, string element) => writer.WriteStartElement(element, SvgNamespace);
    private static void Attr(XmlWriter writer, string name, string value) => writer.WriteAttributeString(name, value);
}
