using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Evakuator.Domain.Entities;
using Evakuator.Domain.Geometry;
using Evakuator.Domain.ValueObjects;
using Evakuator.Geometry;
using Evakuator.Norms.Models;

namespace Evakuator.UI.Controls;

public sealed class PlanCanvas : Control
{
    public static readonly StyledProperty<EvacuationPlan?> PlanProperty = AvaloniaProperty.Register<PlanCanvas, EvacuationPlan?>(nameof(Plan));
    public static readonly StyledProperty<NormDatabaseDto?> DatabaseProperty = AvaloniaProperty.Register<PlanCanvas, NormDatabaseDto?>(nameof(Database));
    public static readonly StyledProperty<IReadOnlySet<string>?> HighlightedIdsProperty = AvaloniaProperty.Register<PlanCanvas, IReadOnlySet<string>?>(nameof(HighlightedIds));
    public EvacuationPlan? Plan { get => GetValue(PlanProperty); set => SetValue(PlanProperty, value); }
    public NormDatabaseDto? Database { get => GetValue(DatabaseProperty); set => SetValue(DatabaseProperty, value); }
    public IReadOnlySet<string>? HighlightedIds { get => GetValue(HighlightedIdsProperty); set => SetValue(HighlightedIdsProperty, value); }
    static PlanCanvas() => AffectsRender<PlanCanvas>(PlanProperty, DatabaseProperty, HighlightedIdsProperty);

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.DrawRectangle(new SolidColorBrush(Color.Parse("#F4F8F7")), null, new Rect(Bounds.Size));
        if (Plan is not { } plan || Database is not { } database || Bounds.Width < 80 || Bounds.Height < 80) return;
        var padding = 32d;
        var scale = Math.Min((Bounds.Width - padding * 2) / plan.Width, (Bounds.Height - padding * 2) / plan.Height);
        if (scale <= 0 || !double.IsFinite(scale)) return;
        var left = (Bounds.Width - plan.Width * scale) / 2;
        var top = (Bounds.Height - plan.Height * scale) / 2;
        Point Map(Point2D p) => new(left + p.X * scale, top + (plan.Height - p.Y) * scale);
        var paper = new Rect(left, top, plan.Width * scale, plan.Height * scale);
        context.DrawRectangle(Brushes.White, new Pen(new SolidColorBrush(Color.Parse("#B9C9C4")), 1), paper);
        foreach (var line in plan.Background.Where(b => b.Points.Length >= 2))
            VectorDrawing.Draw(context, new PolylinePrimitive(line.Points, "#A3B3B0", 36), Map, scale);
        var pathGenerator = new PathGeometryGenerator();
        foreach (var path in plan.Paths)
        {
            foreach (var primitive in pathGenerator.Generate(path, database.Parameters)) VectorDrawing.Draw(context, primitive, Map, scale);
            if (HighlightedIds?.Contains(path.Id) == true && path.Points.Length >= 2)
                VectorDrawing.Draw(context, new PolylinePrimitive(path.Points, "#D9473F", 80, true), Map, scale);
        }
        var generator = new SignGeometryGenerator();
        foreach (var sign in plan.Signs)
        {
            if (database.FindSign(sign.Code) is null || sign.ModelHeight <= 0) continue;
            var recipe = generator.Generate(sign, database);
            foreach (var primitive in recipe.Primitives) VectorDrawing.Draw(context, primitive, Map, scale);
            if (HighlightedIds?.Contains(sign.Id) == true)
            {
                var b = recipe.Bounds;
                VectorDrawing.Draw(context, new PolylinePrimitive([new(b.MinX,b.MinY),new(b.MaxX,b.MinY),new(b.MaxX,b.MaxY),new(b.MinX,b.MaxY),new(b.MinX,b.MinY)], "#D9473F", 60, true), Map, scale);
            }
        }
        foreach (var marker in plan.Markers)
        {
            VectorDrawing.Draw(context, new CirclePrimitive(marker.Position, 180, "#1767B4", "#FFFFFF", 35), Map, scale);
            VectorDrawing.Draw(context, new TextPrimitive(marker.Position.Translate(260, 200), marker.NeedsReview ? "ВЫ ЗДЕСЬ ?" : "ВЫ ЗДЕСЬ", 280, "#264F6C"), Map, scale);
        }
    }
}
