using System.Text.Json;
using System.Text.Json.Serialization;
using Evakuator.Domain.Entities;
using Evakuator.Domain.ValueObjects;

namespace Evakuator.Domain.Serialization;

public static class PlanJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();
    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            WriteIndented = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };
        options.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
        return options;
    }

    public static EvacuationPlan Load(string path)
    {
        var json = File.ReadAllText(path);
        PlanJsonShapeValidator.Validate(json);
        var plan = JsonSerializer.Deserialize<EvacuationPlan>(json, Options)
            ?? throw new InvalidDataException("Plan JSON is empty.");
        CheckStructure(plan);
        return plan;
    }

    public static void Save(string path, EvacuationPlan plan)
    {
        CheckStructure(plan);
        File.WriteAllText(path, JsonSerializer.Serialize(plan, Options));
    }

    public static void CheckStructure(EvacuationPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (string.IsNullOrWhiteSpace(plan.Id) || string.IsNullOrWhiteSpace(plan.Name) ||
            !Enum.IsDefined(plan.Kind) || !double.IsFinite(plan.Width) || !double.IsFinite(plan.Height) || plan.Width <= 0 || plan.Height <= 0)
            throw new InvalidDataException("Plan id, name and positive finite model dimensions are required.");
        if (plan.Background.IsDefault || plan.Signs.IsDefault || plan.Paths.IsDefault || plan.Markers.IsDefault ||
            (plan.Legend is not null && plan.Legend.Entries.IsDefault))
            throw new InvalidDataException("Plan collections cannot be null.");
        if (plan.Legend is not null && plan.Legend.Entries.Any(e => e is null || string.IsNullOrWhiteSpace(e.Key) || e.Description is null))
            throw new InvalidDataException("Invalid legend entry.");
        if (plan.Background.Any(b => b is null || b.Points.IsDefault))
            throw new InvalidDataException("Invalid background geometry.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var s in plan.Signs)
        {
            if (s is null || string.IsNullOrWhiteSpace(s.Id) || !ids.Add(s.Id) || string.IsNullOrWhiteSpace(s.Code) ||
                !double.IsFinite(s.DrawingScale) || s.DrawingScale <= 0 || !double.IsFinite(s.ModelHeight) ||
                !double.IsFinite(s.RotationDegrees))
                throw new InvalidDataException("Invalid sign or duplicate object id.");
        }
        foreach (var p in plan.Paths)
        {
            if (p is null || string.IsNullOrWhiteSpace(p.Id) || !ids.Add(p.Id) || p.Points.IsDefault ||
                !Enum.IsDefined(p.Type) || !Enum.IsDefined(p.LineStyle) || !double.IsFinite(p.LineWeightMmOnPaper) || !double.IsFinite(p.DrawingScale) || p.DrawingScale <= 0)
                throw new InvalidDataException("Invalid path or duplicate object id.");
            try { _ = RgbColor.Parse(p.Color); }
            catch (FormatException ex) { throw new InvalidDataException("Path color must be #RRGGBB.", ex); }
        }
        foreach (var m in plan.Markers)
            if (m is null || string.IsNullOrWhiteSpace(m.Id) || !ids.Add(m.Id))
                throw new InvalidDataException("Invalid marker or duplicate object id.");
    }
}
