using System.Collections.Immutable;
using Evakuator.Domain.Entities;
using Evakuator.Domain.Enums;
using Evakuator.Domain.ValueObjects;
using Evakuator.Geometry;
using Evakuator.Validation.Abstractions;
using Evakuator.Validation.Engine;
using Evakuator.Validation.Rules;

namespace Evakuator.Tests;

public class RuleTests
{
    private static readonly NormController Controller = new();
    private static bool Has(EvacuationPlan plan, string rule) => Controller.Validate(plan, TestData.Database()).Issues.Any(i => i.RuleId == rule && i.Severity == IssueSeverity.Error);
    private static EvacuationPlan WithOneSign(double height, double scale = 100)
    {
        var plan = TestData.CleanPlan();
        return plan with { Signs = [TestData.Sign("one", "F04", 5000, 5000, height, scale)] };
    }

    private sealed class IncorrectRuleId : INormRule
    {
        public string Id => "custom";
        public IEnumerable<ValidationIssue> Evaluate(EvacuationPlan plan, Evakuator.Norms.Models.NormDatabaseDto database)
        { yield return new ValidationIssue("different", plan.Id, IssueSeverity.Error, "Wrong rule identity"); }
    }

    [Fact]
    public void CustomRuleCannotSpoofRuleIdsOrDistortTheScore()
    {
        var controller = new NormController([new IncorrectRuleId()]);
        Assert.Throws<InvalidOperationException>(() => controller.Validate(TestData.CleanPlan(), TestData.Database()));
    }

    [Fact]
    public void CompleteReferencePlanPassesTheSevenPrototypeRules()
    {
        var report = Controller.Validate(TestData.CleanPlan(), TestData.Database());
        Assert.Equal(0, report.ErrorCount);
        Assert.Equal(0, report.WarningCount);
        Assert.Equal(100, report.ComplianceScore);
        Assert.Contains("не сертификат", report.Scope, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(7.99, true)]
    [InlineData(8.00, false)]
    [InlineData(15.00, false)]
    [InlineData(15.01, true)]
    public void R01ChecksInclusivePaperSizeBoundaries(double height, bool expectedError)
        => Assert.Equal(expectedError, Has(WithOneSign(height), "R01"));

    [Theory]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(200)]
    public void R01CalculatesPaperSizeAcrossDrawingScales(double scale)
    {
        Assert.False(Has(WithOneSign(8, scale), "R01"));
        Assert.True(Has(WithOneSign(7.99, scale), "R01"));
    }

    [Fact]
    public void R01ReportsUnknownCodeInsteadOfSilentlyApproving()
        => Assert.True(Has(WithOneSign(10) with { Signs = [TestData.Sign("s", "E98", 100, 100)] }, "R01"));

    [Fact]
    public void R02DetectsDifferentCodesWithSameCarrierAndDifferentSizes()
    {
        var plan = TestData.CleanPlan();
        plan = plan with { Signs = plan.Signs.SetItem(1, plan.Signs[1].WithPaperHeight(14)) };
        Assert.True(Has(plan, "R02"));
    }

    [Fact]
    public void R02AcceptsUniformValidHeightEvenWhenItIsNotDefault()
    {
        var plan = TestData.CleanPlan();
        plan = plan with { Signs = plan.Signs.Select(s => s.WithPaperHeight(12)).ToImmutableArray() };
        Assert.False(Has(plan, "R02"));
    }

    [Fact]
    public void R02UsesConfigurableTolerance()
    {
        var plan = TestData.CleanPlan();
        plan = plan with { Signs = plan.Signs.SetItem(1, plan.Signs[1].WithPaperHeight(10.005)) };
        Assert.False(Has(plan, "R02"));
    }

    [Fact]
    public void R03RequiresAPath()
        => Assert.True(Has(TestData.CleanPlan() with { Paths = [] }, "R03"));

    [Fact]
    public void R03ReportsMissingDirection()
    {
        var p = TestData.CleanPlan();
        Assert.True(Has(p with { Paths = [p.Paths[0] with { HasDirectionArrow = false }] }, "R03"));
    }

    [Fact]
    public void R03ReportsDuplicateConsecutiveVertices()
    {
        var p = TestData.CleanPlan();
        Assert.True(Has(p with { Paths = [p.Paths[0] with { Points = [new(1,1), new(1,1), new(15000,6000)] }] }, "R03"));
    }

    [Fact]
    public void R03ReportsCrossingAndCollinearOverlap()
    {
        Assert.True(PolylineMath.HasSelfIntersection([new(0,0), new(10,10), new(0,10), new(10,0)]));
        Assert.True(PolylineMath.HasSelfIntersection([new(0,0), new(10,0), new(5,0), new(15,0)]));
        Assert.False(PolylineMath.HasSelfIntersection([new(0,0), new(10,0), new(10,10)]));
        var p = TestData.CleanPlan();
        Assert.True(Has(p with { Paths = [p.Paths[0] with { Points = [new(0,0),new(10,10),new(0,10),new(10,0),new(15000,6000)] }] }, "R03"));
    }

    [Fact]
    public void R03ChecksStyleColorThicknessAndEndpoint()
    {
        var p = TestData.CleanPlan();
        var path = p.Paths[0] with { Type = EvacuationPathType.Secondary, LineStyle = PathLineStyle.Solid, Color = "#000000", LineWeightMmOnPaper = .1, Points = [new(1000,5000),new(12000,6000)] };
        var issue = Controller.Validate(p with { Paths = [path] }, TestData.Database()).Issues.First(i => i.RuleId == "R03");
        Assert.Contains("тип линии", issue.Message);
        Assert.Contains("цвет", issue.Message);
        Assert.Contains("толщина", issue.Message);
        Assert.Contains("конец", issue.Message);
    }

    [Fact]
    public void R03AcceptsSecondaryDashedPathWhenItReachesExit()
    {
        var p = TestData.CleanPlan();
        var path = p.Paths[0] with { Type = EvacuationPathType.Secondary, LineStyle = PathLineStyle.Dashed };
        Assert.False(Has(p with { Paths = [path] }, "R03"));
    }

    [Fact]
    public void R04RequiresMarkerAndReportsReviewWarning()
    {
        var p = TestData.CleanPlan();
        Assert.True(Has(p with { Markers = [] }, "R04"));
        var warning = Controller.Validate(p with { Markers = [p.Markers[0] with { NeedsReview = true }] }, TestData.Database()).Issues;
        Assert.Contains(warning, i => i.RuleId == "R04" && i.Severity == IssueSeverity.Warning && i.RequiresHumanReview);
    }

    [Fact]
    public void R05DetectsMissingLegend()
        => Assert.True(Has(TestData.CleanPlan() with { Legend = null }, "R05"));

    [Fact]
    public void R05DetectsMissingForeignAndDuplicatedEntries()
    {
        var p = TestData.CleanPlan();
        var invalid = new LegendTable { Entries = [new("F10", "Шаблон"), new("F10", "Шаблон")] };
        Assert.True(Has(p with { Legend = invalid }, "R05"));
    }

    [Fact]
    public void R05DetectsStaleDescriptionAfterDatabaseEdit()
    {
        var p = TestData.CleanPlan();
        var db = TestData.Database();
        var index = Array.FindIndex(db.Signs.ToArray(), s => s.Code == "F04");
        db = db with { Signs = db.Signs.SetItem(index, db.Signs[index] with { OfficialDescription = "Новое описание" }) };
        Assert.Contains(Controller.Validate(p, db).Issues, i => i.RuleId == "R05");
    }

    [Fact]
    public void R06DetectsDuplicateAndBoundaryViolation()
    {
        var p = TestData.CleanPlan();
        var sign = p.Signs[1];
        p = p with { Signs = p.Signs.Add(sign with { Id = "duplicate" }).Add(TestData.Sign("outside", "F10", 19900, 11000)) };
        var issues = Controller.Validate(p, TestData.Database()).Issues.Where(i => i.RuleId == "R06").ToArray();
        Assert.Contains(issues, i => i.RelatedObjectId == "duplicate");
        Assert.Contains(issues, i => i.ObjectId == "outside");
    }

    [Theory]
    [InlineData(850, false)] // exactly 15% overlap
    [InlineData(849, true)]
    [InlineData(1001, false)]
    public void R06UsesOverlapOfSmallerCarrierAndThreshold(double separation, bool expected)
    {
        var p = TestData.CleanPlan() with { Signs = [TestData.Sign("a", "F04", 1000, 1000), TestData.Sign("b", "F10", 1000 + separation, 1000)] };
        Assert.Equal(expected, Has(p, "R06"));
    }

    [Fact]
    public void R07RejectsLegacyOnlyAndPhoneNumberSubstrings()
    {
        var p = TestData.CleanPlan();
        p = p with { FireInstructions = "При пожаре звоните 01 или 0101 и 0112." };
        Assert.True(Has(p, "R07"));
        Assert.False(R07_MandatoryTextAndHeaderRule.ContainsPhone("0101", "101"));
    }

    [Fact]
    public void R07RequiresBothApprovalAndTitle()
    {
        var p = TestData.CleanPlan();
        Assert.True(Has(p with { Header = new PlanHeader { Approval = "", Title = "План" } }, "R07"));
    }
}
