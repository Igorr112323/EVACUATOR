using Evakuator.Domain.Enums;
using Evakuator.Domain.ValueObjects;
using Evakuator.Norms.Models;

namespace Evakuator.Geometry;

public sealed record FrameLayout(double WidthMm, double HeightMm, BoundingBox2D Drawing, BoundingBox2D Header,
    BoundingBox2D Legend, BoundingBox2D Instructions);

public sealed class FrameLayoutEngine
{
    /// <summary>Prototype dimensions supplied by JSON; not a claim about ISO A2/A3 or legal compliance.</summary>
    public FrameLayout Create(PlanKind kind, NormParametersDto p)
    {
        ArgumentNullException.ThrowIfNull(p);
        var w = kind == PlanKind.Local ? p.LocalFrameWidthMm : p.FloorFrameWidthMm;
        var h = kind == PlanKind.Local ? p.LocalFrameHeightMm : p.FloorFrameHeightMm;
        if (!double.IsFinite(w) || !double.IsFinite(h) || w <= 0 || h <= 0) throw new ArgumentOutOfRangeException(nameof(p));
        return new FrameLayout(w, h,
            new BoundingBox2D(w * .03, h * .20, w * .70, h * .85),
            new BoundingBox2D(w * .03, h * .87, w * .97, h * .97),
            new BoundingBox2D(w * .73, h * .20, w * .97, h * .85),
            new BoundingBox2D(w * .03, h * .03, w * .97, h * .17));
    }
}
