using System.Xml.Linq;
using Evakuator.Geometry;

namespace Evakuator.Tests;

public class SignCatalogExporterTests
{
    [Fact]
    public void DemoCatalogContainsEverySignAndVisibleVerificationWarning()
    {
        var database = TestData.Database();
        var exporter = new SignCatalogExporter();
        var first = exporter.Export(database);
        Assert.Equal(first, exporter.Export(database));
        var document = XDocument.Parse(first);
        XNamespace svg = "http://www.w3.org/2000/svg";
        Assert.Equal(svg + "svg", document.Root!.Name);
        var text = document.Descendants(svg + "text").Select(element => element.Value).ToArray();
        Assert.Equal(30, database.Signs.Length);
        foreach (var sign in database.Signs)
            Assert.Equal(1, text.Count(value => value == sign.Code));
        Assert.Contains(text, value => value.Contains("НЕ ОФИЦИАЛЬНЫЙ ЭТАЛОН", StringComparison.Ordinal));
        Assert.Contains(text, value => value.Contains("[ПРОВЕРИТЬ ПО ТЕКСТУ ГОСТ]", StringComparison.Ordinal));
        Assert.DoesNotContain(document.Descendants(), element => element.Name.LocalName is "image" or "script");
    }
}
