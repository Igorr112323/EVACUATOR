using System.Text;
using Evakuator.Geometry;
using Evakuator.Norms.Services;

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: Evakuator.SignCatalog <norms.json> <output.svg>");
    return 64;
}

try
{
    var database = new JsonNormRepository(args[0]).Load();
    var svg = new SignCatalogExporter().Export(database);
    var output = Path.GetFullPath(args[1]);
    Directory.CreateDirectory(Path.GetDirectoryName(output)!);
    var temporary = output + "." + Guid.NewGuid().ToString("N") + ".tmp";
    try
    {
        File.WriteAllText(temporary, svg, new UTF8Encoding(false));
        File.Move(temporary, output, true);
    }
    finally { if (File.Exists(temporary)) File.Delete(temporary); }
    Console.WriteLine($"Учебный каталог: {database.Signs.Length} знаков; [ПРОВЕРИТЬ ПО ТЕКСТУ ГОСТ].");
    Console.WriteLine(output);
    return 0;
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or Evakuator.Norms.Exceptions.NormDatabaseValidationException)
{
    Console.Error.WriteLine("Ошибка каталога: " + ex.Message);
    return 1;
}
