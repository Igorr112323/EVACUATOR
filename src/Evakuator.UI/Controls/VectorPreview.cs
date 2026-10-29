using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Evakuator.Domain.Geometry;
using Evakuator.Domain.ValueObjects;

namespace Evakuator.UI.Controls;

public sealed class VectorPreview : Control
{
    public static readonly StyledProperty<ParametricRecipe?> RecipeProperty = AvaloniaProperty.Register<VectorPreview, ParametricRecipe?>(nameof(Recipe));
    public ParametricRecipe? Recipe { get => GetValue(RecipeProperty); set => SetValue(RecipeProperty, value); }
    static VectorPreview() => AffectsRender<VectorPreview>(RecipeProperty);

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Recipe is not { } recipe || recipe.Bounds.Width <= 0 || recipe.Bounds.Height <= 0 || Bounds.Width <= 0 || Bounds.Height <= 0) return;
        var scale = Math.Min((Bounds.Width - 4) / recipe.Bounds.Width, (Bounds.Height - 4) / recipe.Bounds.Height);
        if (scale <= 0) return;
        var left = (Bounds.Width - recipe.Bounds.Width * scale) / 2;
        var top = (Bounds.Height - recipe.Bounds.Height * scale) / 2;
        Point Map(Point2D p) => new(left + (p.X - recipe.Bounds.MinX) * scale, top + (recipe.Bounds.MaxY - p.Y) * scale);
        foreach (var primitive in recipe.Primitives) VectorDrawing.Draw(context, primitive, Map, scale);
    }
}
