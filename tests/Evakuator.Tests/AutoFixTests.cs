using Evakuator.Domain.Entities;
using Evakuator.Domain.ValueObjects;
using Evakuator.Validation.Abstractions;
using Evakuator.Validation.Engine;

namespace Evakuator.Tests;

public class AutoFixTests
{
    [Fact]
    public void AllRepairsAreImmutableAndIdempotent()
    {
        var db = TestData.Database();
        var original = TestData.CleanPlan();
        original = original with { Signs = original.Signs.SetItem(1, original.Signs[1].WithPaperHeight(14)), Legend = null, Markers = [] };
        var fixer = new NormAutoFixer();
        var result = fixer.HealAll(original, db);
        Assert.Equal(14, original.Signs[1].HeightMmOnPaper);
        Assert.Null(original.Legend);
        Assert.Empty(original.Markers);
        Assert.Equal(0, result.After.ErrorCount);
        Assert.Equal(100, result.After.ComplianceScore);
        Assert.True(result.Plan.Markers[0].NeedsReview);
        Assert.Contains(result.After.Issues, i => i.RuleId == "R04" && i.RequiresHumanReview);
        Assert.NotEmpty(result.Changes);
        Assert.Empty(fixer.HealAll(result.Plan, db).Changes);
    }

    [Fact]
    public void DoesNotDeleteDifferentRotationsEvenAtTheSamePoint()
    {
        var p = TestData.CleanPlan();
        var sign = p.Signs[1];
        p = p with { Signs = p.Signs.Add(sign with { Id = "rotated", RotationDegrees = 90 }) };
        var result = new NormAutoFixer().HealAll(p, TestData.Database());
        Assert.Equal(3, result.Plan.Signs.Length);
        Assert.Contains(result.Plan.Signs, s => s.Id == "rotated" && s.NeedsReview);
        Assert.DoesNotContain(result.After.Issues, i => i.RuleId == "R06" && i.Severity == IssueSeverity.Error);
    }

    [Fact]
    public void RemovesOnlyExactDuplicateAndLogsWholeObject()
    {
        var p = TestData.CleanPlan();
        p = p with { Signs = p.Signs.Add(p.Signs[1] with { Id = "dupe" }) };
        var result = new NormAutoFixer().HealAll(p, TestData.Database());
        Assert.DoesNotContain(result.Plan.Signs, s => s.Id == "dupe");
        Assert.Contains(result.Changes, c => c.ObjectId == "dupe" && c.Field == "sign" && c.Before.Contains("F04", StringComparison.Ordinal) && c.RequiresHumanReview);
    }

    [Fact]
    public void NormalizationCannotTurnDifferentOriginalSizesIntoADeletedDuplicate()
    {
        var p = TestData.CleanPlan();
        p = p with { Signs = p.Signs.Add(p.Signs[1].WithPaperHeight(14) with { Id = "different-size" }) };
        var result = new NormAutoFixer().HealAll(p, TestData.Database());
        Assert.Contains(result.Plan.Signs, s => s.Id == "different-size" && s.NeedsReview);
        Assert.DoesNotContain(result.Changes, c => c.ObjectId == "different-size" && c.Field == "sign");
    }

    [Fact]
    public void DegenerateRouteIsPreservedWhenRemovingDuplicatesWouldLeaveOneVertex()
    {
        var p = TestData.CleanPlan();
        p = p with { Paths = [p.Paths[0] with { Points = [new(1000, 5000), new(1000, 5000)] }] };
        var result = new NormAutoFixer().HealAll(p, TestData.Database());
        Assert.Equal(2, result.Plan.Paths[0].Points.Length);
        Assert.Contains(result.After.Issues, i => i.RuleId == "R03" && i.Severity == IssueSeverity.Error);
    }

    [Fact]
    public void MovesOverlappingSignAndMarksPositionForReview()
    {
        var p = TestData.CleanPlan();
        p = p with { Signs = p.Signs.Add(TestData.Sign("overlap", "F10", 5100, 8000)) };
        var result = new NormAutoFixer().HealAll(p, TestData.Database());
        Assert.Contains(result.Plan.Signs, s => s.Id == "overlap" && s.NeedsReview && s.InsertionPoint != new Point2D(5100, 8000));
        Assert.DoesNotContain(result.After.Issues, i => i.RuleId == "R06" && i.Severity == IssueSeverity.Error);
    }

    [Fact]
    public void SnapAtThreeMetresRequiresHumanVerification()
    {
        var p = TestData.CleanPlan();
        p = p with { Paths = [p.Paths[0] with { Points = [new(1000,5000), new(12000,6000)], HasDirectionArrow = false }] };
        var result = new NormAutoFixer().HealAll(p, TestData.Database());
        Assert.Equal(new Point2D(15000,6000), result.Plan.Paths[0].Points[^1]);
        Assert.True(result.Plan.Paths[0].HasDirectionArrow);
        Assert.True(result.Plan.Paths[0].NeedsReview);
        Assert.Contains(result.After.Issues, i => i.RuleId == "R03" && i.Severity == IssueSeverity.Warning);
    }

    [Fact]
    public void RefusesToSnapBeyondConfiguredDistance()
    {
        var p = TestData.CleanPlan();
        p = p with { Paths = [p.Paths[0] with { Points = [new(1000,5000), new(10000,6000)] }] };
        var result = new NormAutoFixer().HealAll(p, TestData.Database());
        Assert.Equal(new Point2D(10000,6000), result.Plan.Paths[0].Points[^1]);
        Assert.Contains(result.After.Issues, i => i.RuleId == "R03" && i.Severity == IssueSeverity.Error);
        Assert.NotEmpty(result.ManualActions);
    }

    [Fact]
    public void CannotInventExitOrHealSelfIntersection()
    {
        var p = TestData.CleanPlan();
        p = p with { Signs = [p.Signs[1]], Paths = [p.Paths[0] with { Points = [new(0,0),new(10,10),new(0,10),new(10,0)] }] };
        var result = new NormAutoFixer().HealAll(p, TestData.Database());
        Assert.Contains(result.After.Issues, i => i.RuleId == "R03" && i.Severity == IssueSeverity.Error);
        Assert.Contains(result.ManualActions, x => x.Contains("R03", StringComparison.Ordinal));
        Assert.True(result.After.ComplianceScore < 100);
    }

    [Fact]
    public void UnknownSignIsPreservedAndRemainsAnError()
    {
        var p = TestData.CleanPlan();
        p = p with { Signs = p.Signs.Add(TestData.Sign("unknown", "E98", 1000, 1000)) };
        var result = new NormAutoFixer().HealAll(p, TestData.Database());
        Assert.Contains(result.Plan.Signs, s => s.Id == "unknown");
        Assert.Contains(result.After.Issues, i => i.RuleId == "R01" && i.ObjectId == "unknown");
    }

    [Fact]
    public void MarkerUsesCentroidOfPathStartsButIsNotRepresentedAsVerified()
    {
        var p = TestData.CleanPlan();
        p = p with { Markers = [], Paths = [p.Paths[0], p.Paths[0] with { Id = "p2", Points = [new(3000,5000),new(15000,6000)] }] };
        var result = new NormAutoFixer().HealAll(p, TestData.Database());
        Assert.Equal(new Point2D(2000,5000), result.Plan.Markers[0].Position);
        Assert.True(result.Plan.Markers[0].NeedsReview);
    }

    [Fact]
    public void HeaderFixPreservesExistingTextAndAppendsCurrentNumbers()
    {
        var p = TestData.CleanPlan() with { Header = null, FireInstructions = "При пожаре звоните 01. Особое указание объекта." };
        var result = new NormAutoFixer().HealAll(p, TestData.Database());
        Assert.Contains("Особое указание объекта", result.Plan.FireInstructions);
        Assert.Contains("101", result.Plan.FireInstructions);
        Assert.Contains("112", result.Plan.FireInstructions);
        Assert.Contains(result.Changes, c => c.RuleId == "R07" && c.RequiresHumanReview);
    }

    [Fact]
    public void DisjointPerSignRangesCannotBeFalselyReportedAsFullyFixed()
    {
        var db = TestData.Database();
        var index = Array.FindIndex(db.Signs.ToArray(), s => s.Code == "F04");
        db = db with { Signs = db.Signs.SetItem(index, db.Signs[index] with { MinHeightMm = 16, DefaultHeightMm = 18, MaxHeightMm = 20 }) };
        var result = new NormAutoFixer().HealAll(TestData.CleanPlan(), db);
        Assert.Contains(result.After.Issues, i => i.RuleId == "R02" && i.Severity == IssueSeverity.Error);
        Assert.Contains(result.ManualActions, s => s.Contains("не пересекаются", StringComparison.Ordinal));
    }
}
