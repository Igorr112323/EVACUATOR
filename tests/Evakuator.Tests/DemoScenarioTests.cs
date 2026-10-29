using System.Xml.Linq;
using Evakuator.Domain.Serialization;
using Evakuator.Geometry;
using Evakuator.Validation.Abstractions;
using Evakuator.Validation.Engine;

namespace Evakuator.Tests;

public class DemoScenarioTests
{
    private static string Sample(string name) => Path.Combine(TestData.Root, "samples", name);

    [Fact]
    public void BazaarScenarioCoversEveryRuleAndFixesPrototypeErrors()
    {
        var db = TestData.Database();
        var plan = PlanJson.Load(Sample("bad_bazaar_plan.json"));
        var result = new NormAutoFixer().HealAll(plan, db);
        Assert.Equal(10, result.Before.ErrorCount);
        Assert.Equal(Enumerable.Range(1, 7).Select(i => $"R{i:00}"), result.Before.Issues.Where(i => i.Severity == IssueSeverity.Error).Select(i => i.RuleId).Distinct().Order(StringComparer.Ordinal));
        Assert.Equal(0, result.After.ErrorCount);
        Assert.Equal(100, result.After.ComplianceScore);
        Assert.Equal(2, result.After.WarningCount);
        Assert.NotEmpty(result.ManualActions);
        Assert.True(result.Plan.Paths[0].NeedsReview);
        Assert.True(result.Plan.Markers[0].NeedsReview);
        Assert.Contains(result.Changes, c => c.Field == "hasDirectionArrow" && c.RequiresHumanReview);
        Assert.Empty(new NormAutoFixer().HealAll(result.Plan, db).Changes);
    }

    [Fact]
    public void CliRejectsMalformedCoordinatesWithExitCodeOneWithoutWritingResults()
    {
        var plan = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Sample("reference_clean_plan.json")))!.AsObject();
        plan["signs"]![0]!["insertionPoint"]!.AsObject().Remove("y");
        var folder = Path.Combine(Path.GetTempPath(), "evac-cli-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var path = Path.Combine(folder, "invalid-plan.json");
            var output = Path.Combine(folder, "out");
            File.WriteAllText(path, plan.ToJsonString());
            var code = Evakuator.CliDemo.Program.Main(["--norms", TestData.NormPath, "--plan", path, "--output-dir", output]);
            Assert.Equal(1, code);
            Assert.False(Directory.Exists(output));
        }
        finally { Directory.Delete(folder, true); }
    }

    [Fact]
    public void ReferencePlanHasNoRuleErrorsAndDoesNotChangeWhenHealedAgain()
    {
        var plan = PlanJson.Load(Sample("reference_clean_plan.json"));
        var result = new NormAutoFixer().HealAll(plan, TestData.Database());
        Assert.Equal(0, result.Before.ErrorCount);
        Assert.Equal(2, result.Before.WarningCount); // not an assertion of real-world safety
        Assert.Same(plan, result.Plan);
        Assert.Empty(result.Changes);
    }

    [Fact]
    public void BothBeforeAndAfterSvgAndLegendAreParseableVectors()
    {
        var db = TestData.Database();
        var before = PlanJson.Load(Sample("bad_bazaar_plan.json"));
        var after = new NormAutoFixer().HealAll(before, db).Plan;
        var exporter = new SvgPreviewExporter();
        foreach (var svg in new[] { exporter.ExportPlan(before, db), exporter.ExportPlan(after, db), exporter.ExportLegend(after, db) })
        {
            var xml = XDocument.Parse(svg);
            Assert.Equal("svg", xml.Root!.Name.LocalName);
            Assert.DoesNotContain(xml.Descendants(), e => e.Name.LocalName == "image" || e.Name.LocalName == "script");
            Assert.NotEmpty(xml.Descendants());
        }
    }

    [Theory]
    [InlineData(9, 10, true)]
    [InlineData(10, 11, true)]
    [InlineData(10, 10, false)]
    public void UniformityChecksBothSidesOfDefault(double first, double second, bool expectedError)
    {
        var plan = TestData.CleanPlan();
        plan = plan with { Signs = [plan.Signs[0].WithPaperHeight(first), plan.Signs[1].WithPaperHeight(second)] };
        var result = new NormController().Validate(plan, TestData.Database());
        Assert.Equal(expectedError, result.Issues.Any(i => i.RuleId == "R02" && i.Severity == IssueSeverity.Error));
    }
}
