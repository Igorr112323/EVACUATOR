using System.Text.Json;

namespace Evakuator.Domain.Serialization;

/// <summary>Reject missing or ambiguous coordinates before deserialization can silently default them to zero.</summary>
internal static class PlanJsonShapeValidator
{
    public static void Validate(string json)
    {
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 64 });
        var root = document.RootElement;
        CheckDuplicates(root, "$");
        Require(root, "$", "id", "name", "width", "height");
        VisitArray(root, "signs", (sign, path) =>
        {
            Require(sign, path, "id", "code", "insertionPoint", "modelHeight", "drawingScale");
            RequirePoint(sign.GetProperty("insertionPoint"), path + ".insertionPoint");
        });
        VisitArray(root, "markers", (marker, path) =>
        {
            Require(marker, path, "id", "position");
            RequirePoint(marker.GetProperty("position"), path + ".position");
        });
        VisitArray(root, "paths", (path, prefix) =>
        {
            Require(path, prefix, "id", "points");
            VisitArray(path, "points", RequirePoint, prefix);
        });
        VisitArray(root, "background", (background, prefix) =>
        {
            Require(background, prefix, "points");
            VisitArray(background, "points", RequirePoint, prefix);
        });
    }

    private static void RequirePoint(JsonElement point, string path) => Require(point, path, "x", "y");

    private static void Require(JsonElement element, string path, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object) throw new InvalidDataException(path + " must be an object.");
        foreach (var name in names)
            if (!element.TryGetProperty(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                throw new InvalidDataException($"{path}.{name} is required.");
    }

    private static void VisitArray(JsonElement parent, string name, Action<JsonElement, string> visit, string prefix = "$")
    {
        if (!parent.TryGetProperty(name, out var array)) return;
        if (array.ValueKind != JsonValueKind.Array) throw new InvalidDataException($"{prefix}.{name} must be an array.");
        var i = 0;
        foreach (var item in array.EnumerateArray()) visit(item, $"{prefix}.{name}[{i++}]");
    }

    private static void CheckDuplicates(JsonElement element, string path)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new InvalidDataException($"Duplicate JSON property {path}.{property.Name}.");
                CheckDuplicates(property.Value, path + "." + property.Name);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            var i = 0;
            foreach (var item in element.EnumerateArray()) CheckDuplicates(item, $"{path}[{i++}]");
        }
    }
}
