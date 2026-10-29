using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Evakuator.Domain.Enums;
using Evakuator.Domain.ValueObjects;

namespace Evakuator.Domain.Entities;

/// <summary>An immutable snapshot. Model-space distances are expressed in millimetres.</summary>
public sealed record EvacuationPlan
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public PlanKind Kind { get; init; } = PlanKind.Floor;
    public double Width { get; init; }
    public double Height { get; init; }
    public ImmutableArray<BackgroundPolyline> Background { get; init; } = [];
    public ImmutableArray<EvacuationSign> Signs { get; init; } = [];
    public ImmutableArray<EvacuationPath> Paths { get; init; } = [];
    public ImmutableArray<YouAreHereMarker> Markers { get; init; } = [];
    public LegendTable? Legend { get; init; }
    public PlanHeader? Header { get; init; }
    public string FireInstructions { get; init; } = string.Empty;
}

/// <summary>Illustrative drawing lines; not considered obstacles by the MVP routing rules.</summary>
public sealed record BackgroundPolyline
{
    public ImmutableArray<Point2D> Points { get; init; } = [];
}

public sealed record EvacuationSign
{
    public required string Id { get; init; }
    public required string Code { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    /// <summary>Lower-left corner of the unrotated carrier in model coordinates.</summary>
    public Point2D InsertionPoint { get; init; }
    public double ModelHeight { get; init; }
    public double DrawingScale { get; init; }
    public double RotationDegrees { get; init; }
    public bool IsSyncedWithGroupScale { get; init; } = true;
    public bool NeedsReview { get; init; }
    public string ReviewNote { get; init; } = string.Empty;

    [JsonIgnore]
    public double HeightMmOnPaper => DrawingScale > 0 ? ModelHeight / DrawingScale : double.NaN;
    public EvacuationSign WithPaperHeight(double height) => this with { ModelHeight = new PaperScale(DrawingScale).ToModel(height) };
}

public sealed record EvacuationPath
{
    public required string Id { get; init; }
    public EvacuationPathType Type { get; init; }
    public ImmutableArray<Point2D> Points { get; init; } = [];
    public bool HasDirectionArrow { get; init; }
    public PathLineStyle LineStyle { get; init; } = PathLineStyle.Solid;
    public string Color { get; init; } = "#008855";
    public double LineWeightMmOnPaper { get; init; }
    public double DrawingScale { get; init; } = 100;
    public bool NeedsReview { get; init; }
    public string ReviewNote { get; init; } = string.Empty;
}

public sealed record YouAreHereMarker
{
    public required string Id { get; init; }
    public Point2D Position { get; init; }
    public bool NeedsReview { get; init; }
    public string Note { get; init; } = string.Empty;
}

public sealed record PlanHeader
{
    public string Approval { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
}

public sealed record LegendEntry(string Key, string Description);
public sealed record LegendTable
{
    public ImmutableArray<LegendEntry> Entries { get; init; } = [];
}
