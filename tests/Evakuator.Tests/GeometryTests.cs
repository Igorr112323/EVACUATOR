using System.Collections.Immutable;
using System.Xml.Linq;
using Evakuator.Domain.Entities;
using Evakuator.Domain.Enums;
using Evakuator.Domain.Geometry;
using Evakuator.Domain.ValueObjects;
using Evakuator.Geometry;

namespace Evakuator.Tests;

public class GeometryTests
{
    public static IEnumerable<object[]> Codes => TestData.Database().Signs.Select(s => new object[] { s.Code });

    [Theory]
    [MemberData(nameof(Codes))]
    public void EveryCatalogSignGeneratesVisibleVectors(string code)
    {
        var db = TestData.Database();
        var sign = TestData.Sign("id", code, 10, 20);
        var recipe = new SignGeometryGenerator().Generate(sign, db);
        Assert.True(recipe.Primitives.Length >= 2);
        Assert.Equal(1000, recipe.Bounds.Height, 8);
        Assert.Equal(db.FindSign(code)!.AspectRatio * 1000, recipe.Bounds.Width, 8);
        var xml = XDocument.Parse(new SvgPreviewExporter().ExportSign(sign, db));
        Assert.Equal("svg", xml.Root!.Name.LocalName);
        Assert.DoesNotContain(xml.Descendants(), x => x.Name.LocalName == "image");
    }

    [Fact]
    public void AmendmentArcRendersAsVectorAndRotatesWithTheSign()
    {
        var db = new Evakuator.Norms.Services.JsonNormRepository(TestData.NormPath).Load(Path.Combine(TestData.Root, "norms", "sample_custom_amendment.json"));
        var sign = TestData.Sign("arc", "E99", 0, 0) with { RotationDegrees = 90 };
        var recipe = new SignGeometryGenerator().Generate(sign, db);
        var arc = Assert.Single(recipe.Primitives.OfType<ArcPrimitive>());
        Assert.Equal(350, arc.Radius, 8);
        Assert.Equal(90, arc.StartAngleDegrees, 8);
        Assert.Equal(270, arc.SweepAngleDegrees, 8);
        Assert.Equal(-500, arc.At(0).X, 7);
        Assert.Equal(850, arc.At(0).Y, 7);
        var xml = XDocument.Parse(new SvgPreviewExporter().ExportSign(sign, db));
        var path = Assert.Single(xml.Descendants(), e => e.Name.LocalName == "path");
        Assert.Contains(" A 350 350 0 1 1 ", path.Attribute("d")!.Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(360)]
    [InlineData(-360)]
    public void InvalidArcSweepIsRejected(double sweep)
    {
        var db = new Evakuator.Norms.Services.JsonNormRepository(TestData.NormPath).Load(Path.Combine(TestData.Root, "norms", "sample_custom_amendment.json"));
        var index = Array.FindIndex(db.Signs.ToArray(), d => d.Code == "E99");
        var sign = db.Signs[index];
        var arcIndex = Array.FindIndex(sign.Primitives.ToArray(), p => p.Type == "Arc");
        var bad = sign with { Primitives = sign.Primitives.SetItem(arcIndex, sign.Primitives[arcIndex] with { SweepAngleDegrees = sweep }) };
        Assert.Contains(new Evakuator.Norms.Services.NormDatabaseValidator().Validate(db with { Signs = db.Signs.SetItem(index, bad) }), e => e.Contains("invalid arc", StringComparison.Ordinal));
    }

    [Fact]
    public void RotationRotatesCarrierAndBoundsTogether()
    {
        var sign = TestData.Sign("s", "E22", 2000, 1000) with { RotationDegrees = 90 };
        var bounds = new SignGeometryGenerator().Bounds(sign, TestData.Database());
        Assert.Equal(1000, bounds.MinX, 6);
        Assert.Equal(2000, bounds.MaxX, 6);
        Assert.Equal(1000, bounds.MinY, 6);
        Assert.Equal(3000, bounds.MaxY, 6);
    }

    [Fact]
    public void PathArrowsAreSpacedAcrossSegmentsAndEndAtExit()
    {
        var p = new EvacuationPath { Id = "p", Points = [new(0,0),new(1000,0),new(1000,2000)], HasDirectionArrow = true, LineWeightMmOnPaper = .5 };
        var config = TestData.Database().Parameters with { ArrowSpacingMm = 1000, ArrowSizeMm = 200 };
        var output = new PathGeometryGenerator().Generate(p, config);
        var arrows = output.OfType<PolygonPrimitive>().ToArray();
        Assert.Equal(3, arrows.Length);
        Assert.Equal(new Point2D(1000,2000), arrows[^1].Points[0]);
        Assert.IsType<PolylinePrimitive>(output[0]);
    }

    [Fact]
    public void FinalArrowDoesNotOverlapThePreviousArrow()
    {
        var p = new EvacuationPath { Id = "p", Points = [new(0,0),new(14500,0)], HasDirectionArrow = true };
        var config = TestData.Database().Parameters with { ArrowSpacingMm = 3500, ArrowSizeMm = 500 };
        var arrows = new PathGeometryGenerator().Generate(p, config).OfType<PolygonPrimitive>().ToArray();
        Assert.Equal(4, arrows.Length);
        Assert.Equal(new Point2D(14500,0), arrows[^1].Points[0]);
        Assert.True(arrows[^1].Points[0].X - arrows[^2].Points[0].X >= 875);
    }

    [Fact]
    public void PathWithoutDirectionProducesNoArrows()
    {
        var p = new EvacuationPath { Id = "p", Points = [new(0,0),new(1000,0)], HasDirectionArrow = false, LineWeightMmOnPaper = .5 };
        Assert.Single(new PathGeometryGenerator().Generate(p, TestData.Database().Parameters));
    }

    [Fact]
    public void SecondaryPathIsDashed()
    {
        var p = new EvacuationPath { Id = "p", Type = EvacuationPathType.Secondary, LineStyle = PathLineStyle.Dashed, Points = [new(0,0),new(1000,0)] };
        var line = Assert.IsType<PolylinePrimitive>(new PathGeometryGenerator().Generate(p, TestData.Database().Parameters)[0]);
        Assert.True(line.Dashed);
    }

    [Fact]
    public void LegendIsDerivedFromActualUniqueContents()
    {
        var db = TestData.Database();
        var plan = TestData.CleanPlan();
        plan = plan with { Signs = plan.Signs.Add(TestData.Sign("fire-2", "F04", 10000, 10000)) };
        var legend = new LegendLayoutEngine().Build(plan, db);
        Assert.Equal(1, legend.Entries.Count(e => e.Key == "F04"));
        Assert.DoesNotContain(legend.Entries, e => e.Key == "F10");
        Assert.Contains(legend.Entries, e => e.Key == "PATH:Primary");
        Assert.Contains(legend.Entries, e => e.Key == "HERE");
    }

    [Fact]
    public void FrameDimensionsComeFromEditableConfiguration()
    {
        var config = TestData.Database().Parameters with { LocalFrameWidthMm = 420 };
        var engine = new FrameLayoutEngine();
        Assert.Equal(600, engine.Create(PlanKind.Floor, config).WidthMm);
        Assert.Equal(420, engine.Create(PlanKind.Local, config).WidthMm);
        Assert.Equal(300, engine.Create(PlanKind.Local, config).HeightMm);
    }

    [Fact]
    public void SvgEscapesUntrustedTextAndHasNoExecutableMarkup()
    {
        var db = TestData.Database();
        var index = Array.FindIndex(db.Signs.ToArray(), s => s.Code == "E22");
        var sign = db.Signs[index];
        var primitive = sign.Primitives[0] with { Text = "<script>alert('x')</script>" };
        db = db with { Signs = db.Signs.SetItem(index, sign with { Primitives = ImmutableArray.Create(primitive) }) };
        var xml = XDocument.Parse(new SvgPreviewExporter().ExportSign(TestData.Sign("a", "E22", 0, 0), db));
        Assert.DoesNotContain(xml.Descendants(), e => e.Name.LocalName == "script");
        Assert.Contains(xml.Descendants(), e => e.Name.LocalName == "text" && e.Value.Contains("<script>", StringComparison.Ordinal));
    }
}
