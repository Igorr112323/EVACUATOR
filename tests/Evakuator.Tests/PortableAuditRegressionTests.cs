using System.Globalization;
using Evakuator.Domain.ValueObjects;
using Evakuator.Geometry;
using Evakuator.Validation.Abstractions;
using Evakuator.Validation.Engine;
using Evakuator.Validation.Rules;

namespace Evakuator.Tests;

public class PortableAuditRegressionTests
{
    [Fact]
    public void AdjacentRetracingIsReportedWithoutTreatingOrdinaryCornersAsIntersections()
    {
        Assert.False(PolylineMath.HasSelfIntersection([new(0, 0), new(10, 0), new(20, 0)]));
        Assert.False(PolylineMath.HasSelfIntersection([new(0, 0), new(10, 0), new(10, 10)]));
        Assert.True(PolylineMath.HasSelfIntersection([new(0, 0), new(10, 0), new(5, 0)]));
        Assert.True(PolylineMath.HasSelfIntersection([new(0, 0), new(10, 0), new(-5, 0)]));

        var plan = TestData.CleanPlan();
        var path = plan.Paths[0] with
        {
            Points = [new Point2D(1000, 6000), new Point2D(18000, 6000), new Point2D(15000, 6000)]
        };
        var report = new NormController().Validate(plan with { Paths = [path] }, TestData.Database());
        Assert.Contains(report.Issues, issue => issue.RuleId == "R03" && issue.Severity == IssueSeverity.Error &&
            issue.Message.Contains("самопересечение", StringComparison.Ordinal));
    }

    [Fact]
    public void R01FractionalBoundsMessagesAreIndependentOfCurrentCulture()
    {
        var database = TestData.Database();
        var index = Array.FindIndex(database.Signs.ToArray(), sign => sign.Code == "F04");
        Assert.True(index >= 0);
        database = database with
        {
            Signs = database.Signs.SetItem(index, database.Signs[index] with { MinHeightMm = 8.25, MaxHeightMm = 15.75 })
        };
        var plan = TestData.CleanPlan() with { Signs = [TestData.Sign("s", "F04", 5000, 5000, 5)] };
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            var english = Assert.Single(new R01_SignSizeBoundsRule().Evaluate(plan, database)).Message;
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
            var russian = Assert.Single(new R01_SignSizeBoundsRule().Evaluate(plan, database)).Message;
            Assert.Contains("8.25–15.75", english);
            Assert.Equal(english, russian);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public void R06OverlapMessagesAreIndependentOfCurrentCulture()
    {
        var database = TestData.Database();
        var plan = TestData.CleanPlan() with
        {
            Signs = [TestData.Sign("a", "F04", 1000, 1000), TestData.Sign("b", "F10", 1800, 1000)]
        };
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            var english = Assert.Single(new R06_SignCollisionAndOverlapRule().Evaluate(plan, database)).Message;
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
            var russian = Assert.Single(new R06_SignCollisionAndOverlapRule().Evaluate(plan, database)).Message;
            Assert.Contains("20", english);
            Assert.Equal(english, russian);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }
}
