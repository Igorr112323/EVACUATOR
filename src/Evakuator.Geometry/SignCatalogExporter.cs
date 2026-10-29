using System.Collections.Immutable;
using System.Globalization;
using System.Xml.Linq;
using Evakuator.Domain.Entities;
using Evakuator.Norms.Models;

namespace Evakuator.Geometry;

/// <summary>Creates a clearly labelled demo catalog using the same vector renderer as the plan legend.</summary>
public sealed class SignCatalogExporter
{
    public string Export(NormDatabaseDto database)
    {
        ArgumentNullException.ThrowIfNull(database);
        var signs = database.Signs.Select(definition => new EvacuationSign
        {
            Id = definition.Code,
            Code = definition.Code,
            ModelHeight = 1,
            DrawingScale = 1
        }).ToImmutableArray();
        var plan = new EvacuationPlan { Id = "demo-catalog", Name = "Учебный каталог", Width = 1, Height = 1, Signs = signs };
        var document = XDocument.Parse(new SvgPreviewExporter().ExportLegend(plan, database));
        var root = document.Root ?? throw new InvalidOperationException("SVG root is missing.");
        XNamespace svg = "http://www.w3.org/2000/svg";
        var count = signs.Select(sign => sign.Code).Distinct(StringComparer.Ordinal).Count();
        const int warningHeight = 80;
        var height = 80 + 72 * count + warningHeight;
        root.SetAttributeValue("viewBox", $"0 -{warningHeight.ToString(CultureInfo.InvariantCulture)} 900 {height.ToString(CultureInfo.InvariantCulture)}");
        var warning = new XElement(svg + "g",
            new XAttribute("font-family", "sans-serif"), new XAttribute("fill", "#9A3412"),
            new XElement(svg + "text", new XAttribute("x", "20"), new XAttribute("y", "-48"),
                new XAttribute("font-size", "20"), new XAttribute("font-weight", "bold"),
                "УЧЕБНЫЙ КАТАЛОГ — НЕ ОФИЦИАЛЬНЫЙ ЭТАЛОН ЗНАКОВ"),
            new XElement(svg + "text", new XAttribute("x", "20"), new XAttribute("y", "-20"),
                new XAttribute("font-size", "16"),
                "[ПРОВЕРИТЬ ПО ТЕКСТУ ГОСТ] изображения, коды, размеры и назначения."));
        var title = root.Element(svg + "title");
        if (title is null) root.AddFirst(warning);
        else title.AddAfterSelf(warning);
        return document.ToString(SaveOptions.None);
    }
}
