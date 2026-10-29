using System.Text.Json;
using Evakuator.Norms.Exceptions;

namespace Evakuator.Norms.Services;

/// <summary>Enforces required JSON fields before DTO defaults can hide an omitted property.</summary>
internal static class NormJsonShapeValidator
{
    private static readonly string[] SignFields = ["code", "name", "officialDescription", "category", "shape", "aspectRatio",
        "minHeightMm", "maxHeightMm", "defaultHeightMm", "gostClauseRef", "backgroundColor", "isExit", "primitives"];
    private static readonly string[] ParameterFields = ["defaultHeightMm", "uniformToleranceMm", "snapToleranceMm", "maxAutoSnapDistanceMm",
        "pathLineWeightMm", "pathColor", "arrowSpacingMm", "arrowSizeMm", "overlapThreshold", "offsetStepMm", "maxOffsetSteps",
        "duplicateToleranceMm", "floorFrameWidthMm", "floorFrameHeightMm", "localFrameWidthMm", "localFrameHeightMm"];
    private static readonly string[] InstructionFields = ["approval", "title", "instructions"];

    public static void Validate(string json, bool amendment)
    {
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 64 });
        var errors = new List<string>();
        var root = document.RootElement;
        InspectDuplicates(root, "$", errors);
        if (!Require(root, "$", amendment ? [] : ["version", "notice", "parameters", "fireInstructionTemplate", "signs"], errors))
            throw new NormDatabaseValidationException(errors);
        if (amendment && !root.TryGetProperty("signs", out _) && !root.TryGetProperty("parameters", out _) && !root.TryGetProperty("fireInstructionTemplate", out _))
            errors.Add("amendment must contain signs, parameters or fireInstructionTemplate");
        if (root.TryGetProperty("parameters", out var parameters)) Require(parameters, "$.parameters", ParameterFields, errors);
        if (root.TryGetProperty("fireInstructionTemplate", out var instruction)) Require(instruction, "$.fireInstructionTemplate", InstructionFields, errors);
        if (root.TryGetProperty("signs", out var signs))
        {
            if (signs.ValueKind != JsonValueKind.Array) errors.Add("$.signs must be an array");
            else
            {
                if (signs.GetArrayLength() > 5000) errors.Add("$.signs exceeds the safety limit");
                var i = 0;
                foreach (var sign in signs.EnumerateArray())
                {
                    var prefix = $"$.signs[{i++}]";
                    if (!Require(sign, prefix, SignFields, errors) || !sign.TryGetProperty("primitives", out var primitives)) continue;
                    if (primitives.ValueKind != JsonValueKind.Array) { errors.Add(prefix + ".primitives must be an array"); continue; }
                    if (primitives.GetArrayLength() > 2000) errors.Add(prefix + ".primitives exceeds the safety limit");
                    var j = 0;
                    foreach (var primitive in primitives.EnumerateArray())
                    {
                        var path = $"{prefix}.primitives[{j++}]";
                        if (!Require(primitive, path, ["type"], errors) || !primitive.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String) continue;
                        var fields = type.GetString() switch
                        {
                            "Rect" => new[] { "x", "y", "width", "height" },
                            "Circle" => new[] { "x", "y", "radius" },
                            "Arc" => new[] { "x", "y", "radius", "startAngleDegrees", "sweepAngleDegrees", "stroke", "strokeWidth" },
                            "Polygon" or "Polyline" => new[] { "points" },
                            "Text" => new[] { "x", "y", "text", "fontSize" },
                            _ => Array.Empty<string>()
                        };
                        Require(primitive, path, fields, errors);
                        if (primitive.TryGetProperty("points", out var points))
                        {
                            if (points.ValueKind != JsonValueKind.Array) errors.Add(path + ".points must be an array");
                            else
                            {
                                if (points.GetArrayLength() > 10000) errors.Add(path + ".points exceeds the safety limit");
                                var k = 0;
                                foreach (var point in points.EnumerateArray()) Require(point, $"{path}.points[{k++}]", ["x", "y"], errors);
                            }
                        }
                    }
                }
            }
        }
        if (errors.Count > 0) throw new NormDatabaseValidationException(errors);
    }

    private static bool Require(JsonElement element, string path, string[] properties, List<string> errors)
    {
        if (element.ValueKind != JsonValueKind.Object) { errors.Add(path + " must be an object"); return false; }
        foreach (var property in properties)
            if (!element.TryGetProperty(property, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                errors.Add($"{path}.{property} is required");
        return true;
    }

    private static void InspectDuplicates(JsonElement element, string path, List<string> errors)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) errors.Add($"{path}.{property.Name}: duplicate JSON property");
                InspectDuplicates(property.Value, path + "." + property.Name, errors);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            var i = 0;
            foreach (var child in element.EnumerateArray()) InspectDuplicates(child, $"{path}[{i++}]", errors);
        }
    }
}
