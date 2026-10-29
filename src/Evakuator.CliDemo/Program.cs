using System.Text.Json;
using System.Text.Json.Serialization;
using Evakuator.Domain.Serialization;
using Evakuator.Geometry;
using Evakuator.Norms.Services;
using Evakuator.Validation.Abstractions;
using Evakuator.Validation.Engine;

namespace Evakuator.CliDemo;

internal static class Program
{
    private static readonly JsonSerializerOptions ReportJson = CreateOptions();

    public static int Main(string[] args)
    {
        try
        {
            if (args.Contains("--help", StringComparer.Ordinal))
            {
                Console.WriteLine("Evakuator demo: [--norms FILE] [--amendment FILE] [--plan FILE] [--output-dir DIR]");
                Console.WriteLine("Индекс отражает только учебные правила; результаты требуют проверки специалистом.");
                return 0;
            }
            var options = ParseArgs(args);
            var normsPath = options.GetValueOrDefault("--norms") ?? Find("norms/gost_12_4_026_2015.json");
            var planPath = options.GetValueOrDefault("--plan") ?? Find("samples/bad_bazaar_plan.json");
            var output = Path.GetFullPath(options.GetValueOrDefault("--output-dir") ?? "artifacts");
            var db = new JsonNormRepository(normsPath).Load(options.GetValueOrDefault("--amendment"));
            var plan = PlanJson.Load(planPath);
            var fixer = new NormAutoFixer();
            var result = fixer.HealAll(plan, db);
            var exporter = new SvgPreviewExporter();
            var highlighted = result.Before.Issues.Where(i => i.Severity == IssueSeverity.Error)
                .SelectMany(i => i.RelatedObjectId is null ? new[] { i.ObjectId } : new[] { i.ObjectId, i.RelatedObjectId })
                .ToHashSet(StringComparer.Ordinal);
            var afterHighlights = result.After.Issues.Where(i => i.Severity == IssueSeverity.Error)
                .SelectMany(i => i.RelatedObjectId is null ? new[] { i.ObjectId } : new[] { i.ObjectId, i.RelatedObjectId })
                .ToHashSet(StringComparer.Ordinal);
            var files = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["demo_before.svg"] = exporter.ExportPlan(plan, db, highlighted),
                ["validation_before.json"] = JsonSerializer.Serialize(result.Before, ReportJson),
                ["demo_after.svg"] = exporter.ExportPlan(result.Plan, db, afterHighlights),
                ["legend_table.svg"] = exporter.ExportLegend(result.Plan, db),
                ["validation_after.json"] = JsonSerializer.Serialize(result.After, ReportJson),
                ["healing_diff.json"] = JsonSerializer.Serialize(new { result.Changes, result.ManualActions }, ReportJson),
                ["demo_after_plan.json"] = JsonSerializer.Serialize(result.Plan, PlanJson.Options)
            };
            Directory.CreateDirectory(output);
            foreach (var (name, contents) in files) WriteAtomically(Path.Combine(output, name), contents);
            Console.WriteLine("ЭВАКУАТОР — учебный сценарий. Не является проверкой соответствия ГОСТ.");
            Console.WriteLine($"До: {result.Before.ComplianceScore:0.##}% правил, ошибок: {result.Before.ErrorCount}; категории: " +
                string.Join(", ", result.Before.Issues.Where(i => i.Severity == IssueSeverity.Error).Select(i => i.RuleId).Distinct().Order(StringComparer.Ordinal)));
            Console.WriteLine($"После: {result.After.ComplianceScore:0.##}% правил, ошибок: {result.After.ErrorCount}, предупреждений: {result.After.WarningCount}.");
            Console.WriteLine($"Изменений: {result.Changes.Length}; действий для специалиста: {result.ManualActions.Length}.");
            Console.WriteLine("Маршруты, положение маркера/знаков, тексты и нормы обязательно проверяет проектировщик.");
            Console.WriteLine($"Артефакты: {output}");
            return result.After.ErrorCount == 0 ? 0 : 2;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or Evakuator.Norms.Exceptions.NormDatabaseValidationException or JsonException)
        {
            Console.Error.WriteLine("Ошибка демонстрации: " + ex.Message);
            return 1;
        }
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static Dictionary<string, string> ParseArgs(string[] args)
    {
        var allowed = new HashSet<string>(["--norms", "--amendment", "--plan", "--output-dir"], StringComparer.Ordinal);
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < args.Length; i++)
        {
            if (!allowed.Contains(args[i]) || i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException($"Неизвестный аргумент или отсутствует значение: {args[i]}. Используйте --help.");
            if (!result.TryAdd(args[i], args[i + 1])) throw new ArgumentException($"Аргумент указан дважды: {args[i]}.");
            i++;
        }
        return result;
    }

    private static string Find(string relative)
    {
        foreach (var startingPoint in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var current = new DirectoryInfo(startingPoint);
            while (current is not null)
            {
                var candidate = Path.Combine(current.FullName, relative);
                if (File.Exists(candidate)) return candidate;
                current = current.Parent;
            }
        }
        throw new FileNotFoundException($"Не найден {relative}; задайте путь явно.");
    }

    private static void WriteAtomically(string path, string contents)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temporary, contents); File.Move(temporary, path, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
