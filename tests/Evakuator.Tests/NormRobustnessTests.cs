using System.Text.Json.Nodes;
using Evakuator.Norms.Exceptions;
using Evakuator.Norms.Services;

namespace Evakuator.Tests;

public class NormRobustnessTests
{
    [Theory]
    [InlineData("parameters")]
    [InlineData("fireInstructionTemplate")]
    [InlineData("signs")]
    public void ExplicitNullRootValuesAreReportedAsInvalidDatabase(string property)
    {
        var root = JsonNode.Parse(File.ReadAllText(TestData.NormPath))!.AsObject();
        root[property] = null;
        WithTemporaryJson(root.ToJsonString(), file => Assert.Throws<NormDatabaseValidationException>(() => new JsonNormRepository(file).Load()));
    }

    [Fact]
    public void MissingOrNullRequiredFieldsCannotBeHiddenByDtoDefaults()
    {
        var root = JsonNode.Parse(File.ReadAllText(TestData.NormPath))!.AsObject();
        root["signs"]![0]!.AsObject().Remove("isExit");
        WithTemporaryJson(root.ToJsonString(), file => Assert.Throws<NormDatabaseValidationException>(() => new JsonNormRepository(file).Load()));
        root["signs"]![0]!["isExit"] = null;
        WithTemporaryJson(root.ToJsonString(), file => Assert.Throws<NormDatabaseValidationException>(() => new JsonNormRepository(file).Load()));
    }

    [Fact]
    public void DuplicateJsonPropertiesAreRejectedInsteadOfTakingTheLastValue()
    {
        var raw = File.ReadAllText(TestData.NormPath).Replace("\"version\":", "\"version\":\"tampered\", \"version\":", StringComparison.Ordinal);
        WithTemporaryJson(raw, file => Assert.Throws<NormDatabaseValidationException>(() => new JsonNormRepository(file).Load()));
    }

    [Fact]
    public void NullNestedFieldsAreReportedInsteadOfNullReferenceExceptions()
    {
        var db = TestData.Database();
        var invalid = db with { FireInstructionTemplate = db.FireInstructionTemplate with { Instructions = null! } };
        var errors = new NormDatabaseValidator().Validate(invalid);
        Assert.Contains(errors, e => e.Contains("template", StringComparison.Ordinal));
        var sign = db.Signs[0] with { GostClauseRef = null!, Code = null! };
        Assert.NotEmpty(new NormDatabaseValidator().Validate(db with { Signs = db.Signs.SetItem(0, sign) }));
    }

    [Fact]
    public void InvisibleRecipeIsRejected()
    {
        var db = TestData.Database();
        var sign = db.Signs[0];
        var bad = sign.Primitives[0] with { Fill = "none", Stroke = "none", StrokeWidth = 0 };
        Assert.Contains(new NormDatabaseValidator().Validate(db with { Signs = db.Signs.SetItem(0, sign with { Primitives = [bad] }) }), e => e.Contains("invisible", StringComparison.Ordinal));
    }

    private static void WithTemporaryJson(string json, Action<string> test)
    {
        var file = Path.Combine(Path.GetTempPath(), "evac-json-" + Guid.NewGuid().ToString("N") + ".json");
        try { File.WriteAllText(file, json); test(file); }
        finally { File.Delete(file); }
    }
}
