using System.Collections.Immutable;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Evakuator.Domain.Serialization;
using Evakuator.Domain.ValueObjects;
using Evakuator.Geometry;
using Evakuator.Validation.Engine;
using Xunit.Abstractions;

namespace Evakuator.Tests;

public class PortableStressTests
{
    private readonly ITestOutputHelper output;
    public PortableStressTests(ITestOutputHelper output) => this.output = output;

    [Fact]
    public void TwoHundredSignsHealDeterministicallyAndSecondPassIsIdempotent()
    {
        const int count = 200;
        var database = TestData.Database();
        var signs = Enumerable.Range(0, count).Select(i => TestData.Sign(
            $"grid-{i:D3}", i == 0 ? "E01-01" : "F04", 1000 + i % 20 * 1400, 1000 + i / 20 * 1400)).ToImmutableArray();
        signs = signs.SetItem(1, signs[1] with { InsertionPoint = signs[0].InsertionPoint });
        var initial = TestData.CleanPlan();
        var plan = initial with
        {
            Width = 32000,
            Height = 18000,
            Signs = signs,
            Paths = [initial.Paths[0] with { Points = [new Point2D(100, 1000), new Point2D(1000, 1000)] }]
        };
        plan = plan with { Legend = new LegendLayoutEngine().Build(plan, database) };
        var timer = Stopwatch.StartNew();
        var fixer = new NormAutoFixer();
        var first = fixer.HealAll(plan, database);
        var second = fixer.HealAll(plan, database);
        var repeat = fixer.HealAll(first.Plan, database);
        var firstJson = JsonSerializer.Serialize(first.Plan, PlanJson.Options);
        var secondJson = JsonSerializer.Serialize(second.Plan, PlanJson.Options);
        var firstReport = JsonSerializer.Serialize(first.After, PlanJson.Options);
        var secondReport = JsonSerializer.Serialize(second.After, PlanJson.Options);
        var svg = new SvgPreviewExporter().ExportPlan(first.Plan, database);
        var svgAgain = new SvgPreviewExporter().ExportPlan(second.Plan, database);
        timer.Stop();

        Assert.Equal(count, first.Plan.Signs.Length);
        Assert.NotEmpty(first.Changes);
        Assert.Equal(firstJson, secondJson);
        Assert.Equal(firstReport, secondReport);
        Assert.Equal(svg, svgAgain);
        Assert.Empty(repeat.Changes);
        Assert.Equal(firstJson, JsonSerializer.Serialize(repeat.Plan, PlanJson.Options));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(svg))).ToLowerInvariant();
        output.WriteLine($"signs={count}; repeated-heal=2; second-pass-changes={repeat.Changes.Length}; elapsed-ms={timer.ElapsedMilliseconds}; svg-sha256={hash}");
    }
}
