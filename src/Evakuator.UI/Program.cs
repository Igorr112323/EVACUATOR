using Avalonia;
using System.Text.Json;
using Evakuator.Norms.Exceptions;
using Evakuator.CadAdapter.Simulation;
using Evakuator.Domain.Serialization;
using Evakuator.Norms.Services;
using Evakuator.UI.ViewModels;

namespace Evakuator.UI;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            AppPaths.Initialize(args);
            if (args.Contains("--self-check", StringComparer.Ordinal))
            {
                var repository = new JsonNormRepository(AppPaths.Norms);
                var db = repository.Load();
                var context = new InMemoryCadDocumentContext(PlanJson.Load(AppPaths.Plan));
                using var vm = new MainPaletteViewModel(context, () => db);
                Console.WriteLine($"Avalonia ViewModels: {vm.Catalog.TotalCount} signs, {vm.Inspector.ErrorCount} errors, score {vm.Inspector.ScoreText}.");
                return 0;
            }
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            return 0;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or NormDatabaseValidationException or DllNotFoundException or ArgumentException or InvalidOperationException)
        {
            Console.Error.WriteLine("Evakuator UI: " + ex.Message);
            return 1;
        }
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
}

internal static class AppPaths
{
    public static string Norms { get; private set; } = string.Empty;
    public static string Plan { get; private set; } = string.Empty;
    public static void Initialize(string[] args)
    {
        static string? Argument(string[] values, string key)
        {
            var index = Array.IndexOf(values, key);
            if (index < 0) return null;
            if (index + 1 >= values.Length) throw new ArgumentException($"{key} requires a path.");
            return values[index + 1];
        }
        Norms = Path.GetFullPath(Argument(args, "--norms") ?? Find("norms/gost_12_4_026_2015.json"));
        Plan = Path.GetFullPath(Argument(args, "--plan") ?? Find("samples/bad_bazaar_plan.json"));
    }
    private static string Find(string relative)
    {
        foreach (var startingPoint in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var current = new DirectoryInfo(startingPoint);
            while (current is not null)
            {
                var path = Path.Combine(current.FullName, relative);
                if (File.Exists(path)) return path;
                current = current.Parent;
            }
        }
        throw new FileNotFoundException($"Cannot find {relative}; pass --norms / --plan explicitly.");
    }
}
