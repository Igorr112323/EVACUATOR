using System.Collections.Immutable;
using Evakuator.Domain.Enums;
using Evakuator.Domain.ValueObjects;

namespace Evakuator.Norms.Models;

public enum SignShape { Square, Rectangle2x1, CircleSlash, EquilateralTriangle }

public sealed record NormDatabaseDto
{
    public string Version { get; init; } = string.Empty;
    public string Notice { get; init; } = string.Empty;
    public NormParametersDto Parameters { get; init; } = new();
    public FireInstructionTemplateDto FireInstructionTemplate { get; init; } = new();
    public ImmutableArray<SignDefinitionDto> Signs { get; init; } = [];

    public SignDefinitionDto? FindSign(string code) =>
        Signs.FirstOrDefault(s => string.Equals(s.Code, code, StringComparison.Ordinal));
}

public sealed record SignDefinitionDto
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string OfficialDescription { get; init; } = string.Empty;
    public SignCategory Category { get; init; }
    public SignShape Shape { get; init; }
    public double AspectRatio { get; init; }
    public double MinHeightMm { get; init; }
    public double MaxHeightMm { get; init; }
    public double DefaultHeightMm { get; init; }
    public string GostClauseRef { get; init; } = string.Empty;
    public string BackgroundColor { get; init; } = string.Empty;
    public bool IsExit { get; init; }
    public ImmutableArray<VectorPrimitiveDto> Primitives { get; init; } = [];
}

/// <summary>Coordinates, sizes, and stroke widths are fractions of the carrier, in [0,1].</summary>
public sealed record VectorPrimitiveDto
{
    public string Type { get; init; } = string.Empty;
    public ImmutableArray<Point2D> Points { get; init; } = [];
    public double X { get; init; }
    public double Y { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }
    public double Radius { get; init; }
    public double StartAngleDegrees { get; init; }
    public double SweepAngleDegrees { get; init; }
    public double StrokeWidth { get; init; }
    public double FontSize { get; init; }
    public string Fill { get; init; } = "none";
    public string Stroke { get; init; } = "none";
    public string Text { get; init; } = string.Empty;
}

public sealed record NormParametersDto
{
    public double DefaultHeightMm { get; init; }
    public double UniformToleranceMm { get; init; }
    public double SnapToleranceMm { get; init; }
    public double MaxAutoSnapDistanceMm { get; init; }
    public double PathLineWeightMm { get; init; }
    public string PathColor { get; init; } = string.Empty;
    public double ArrowSpacingMm { get; init; }
    public double ArrowSizeMm { get; init; }
    public double OverlapThreshold { get; init; }
    public double OffsetStepMm { get; init; }
    public int MaxOffsetSteps { get; init; }
    public double DuplicateToleranceMm { get; init; }
    public double FloorFrameWidthMm { get; init; }
    public double FloorFrameHeightMm { get; init; }
    public double LocalFrameWidthMm { get; init; }
    public double LocalFrameHeightMm { get; init; }
}

public sealed record FireInstructionTemplateDto
{
    public string Approval { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Instructions { get; init; } = string.Empty;
}

public sealed record NormAmendmentDto
{
    public ImmutableArray<SignDefinitionDto> Signs { get; init; } = [];
    public NormParametersDto? Parameters { get; init; }
    public FireInstructionTemplateDto? FireInstructionTemplate { get; init; }
}
