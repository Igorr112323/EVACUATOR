using Evakuator.Domain.Entities;
using Evakuator.Domain.ValueObjects;
using Evakuator.Domain.Serialization;
using System.Text.Json.Nodes;

namespace Evakuator.Tests;

public class DomainTests
{
    [Theory]
    [InlineData(50, 500)]
    [InlineData(100, 1000)]
    [InlineData(200, 2000)]
    public void PaperScaleConvertsBothWays(double scale, double model)
    {
        var unit = new PaperScale(scale);
        Assert.Equal(model, unit.ToModel(10));
        Assert.Equal(10, unit.ToPaper(model));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ScaleRejectsInvalidValues(double value) => Assert.Throws<ArgumentOutOfRangeException>(() => new PaperScale(value));

    [Fact]
    public void RotationPreservesDistanceAndUsesDegrees()
    {
        var p = new Point2D(2, 1).RotateAround(new Point2D(1, 1), 90);
        Assert.Equal(1, p.X, 10);
        Assert.Equal(2, p.Y, 10);
    }

    [Fact]
    public void OverlapMeasuresSmallerAreaSoContainedSignsAreNotMissed()
    {
        var big = new BoundingBox2D(0, 0, 10, 10);
        Assert.Equal(1, big.OverlapOfSmaller(new BoundingBox2D(1, 1, 2, 2)));
        Assert.Equal(0, big.OverlapOfSmaller(new BoundingBox2D(10, 10, 11, 11)));
    }

    [Fact]
    public void CodeNormalizationRejectsGarbage()
    {
        Assert.Equal("E01-01", new SignCode(" e01-01 ").Value);
        Assert.Throws<ArgumentException>(() => new SignCode("E/../01"));
    }

    [Fact]
    public void SignSizeUsesModelSpaceAndScale()
    {
        var sign = new EvacuationSign { Id = "1", Code = "F04", DrawingScale = 100, ModelHeight = 1400 };
        Assert.Equal(14, sign.HeightMmOnPaper);
        Assert.Equal(1000, sign.WithPaperHeight(10).ModelHeight);
        Assert.Equal(1400, sign.ModelHeight); // immutable original
    }

    [Theory]
    [InlineData("signs", "insertionPoint")]
    [InlineData("markers", "position")]
    public void MissingPointCoordinateIsNotSilentlyConvertedToZero(string array, string point)
    {
        var sample = Path.Combine(TestData.Root, "samples", "reference_clean_plan.json");
        var root = JsonNode.Parse(File.ReadAllText(sample))!.AsObject();
        root[array]![0]![point]!.AsObject().Remove("y");
        WithTemporaryPlan(root.ToJsonString(), path => Assert.Throws<InvalidDataException>(() => PlanJson.Load(path)));
    }

    [Fact]
    public void DuplicatePlanJsonPropertiesAreRejected()
    {
        var sample = Path.Combine(TestData.Root, "samples", "reference_clean_plan.json");
        var json = File.ReadAllText(sample).Replace("\"width\":", "\"width\":123, \"width\":", StringComparison.Ordinal);
        WithTemporaryPlan(json, path => Assert.Throws<InvalidDataException>(() => PlanJson.Load(path)));
    }

    private static void WithTemporaryPlan(string json, Action<string> test)
    {
        var file = Path.Combine(Path.GetTempPath(), "evac-plan-" + Guid.NewGuid().ToString("N") + ".json");
        try { File.WriteAllText(file, json); test(file); }
        finally { File.Delete(file); }
    }

    [Fact]
    public void ColorParserChecksInput() {
        Assert.Equal("#008855", RgbColor.Parse("#008855").ToString());
        Assert.Throws<FormatException>(() => RgbColor.Parse("#hello!"));
    }
}
