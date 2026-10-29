using System.Collections.ObjectModel;
using Evakuator.Domain.Entities;
using Evakuator.Domain.Geometry;
using Evakuator.Domain.ValueObjects;
using Evakuator.Geometry;
using Evakuator.Norms.Models;

namespace Evakuator.UI.ViewModels;

public sealed record SignCatalogItem(string Code, string Name, string Category, ParametricRecipe Preview, string Description, string Reference);

public sealed class SignCatalogViewModel : ObservableObject
{
    private string search = string.Empty;
    private string selectedCategory = "Все";
    private SignCatalogItem? selectedSign;
    private IReadOnlyList<SignCatalogItem> all = [];
    public IReadOnlyList<string> Categories { get; } = ["Все", "E", "F", "EC", "P", "W"];
    public ObservableCollection<SignCatalogItem> FilteredSigns { get; } = [];
    public int TotalCount => all.Count;
    public string Search
    {
        get => search;
        set { if (Set(ref search, value ?? string.Empty)) Filter(); }
    }
    public string SelectedCategory
    {
        get => selectedCategory;
        set { if (Set(ref selectedCategory, value ?? "Все")) Filter(); }
    }
    public SignCatalogItem? SelectedSign { get => selectedSign; set => Set(ref selectedSign, value); }

    public SignCatalogViewModel(NormDatabaseDto database) => UpdateDatabase(database);

    public void UpdateDatabase(NormDatabaseDto database)
    {
        var generator = new SignGeometryGenerator();
        all = database.Signs.Select(def =>
        {
            var sign = new EvacuationSign { Id = "preview", Code = def.Code, InsertionPoint = new Point2D(0, 0), ModelHeight = 100, DrawingScale = 1 };
            return new SignCatalogItem(def.Code, def.Name, def.Category.ToString(), generator.Generate(sign, database), def.OfficialDescription, def.GostClauseRef);
        }).ToArray();
        Raise(nameof(TotalCount));
        Filter();
    }

    private void Filter()
    {
        var selectedCode = SelectedSign?.Code;
        var found = all.Where(s => (SelectedCategory == "Все" || s.Category == SelectedCategory) &&
            (string.IsNullOrWhiteSpace(Search) || s.Code.Contains(Search.Trim(), StringComparison.OrdinalIgnoreCase) ||
             s.Name.Contains(Search.Trim(), StringComparison.CurrentCultureIgnoreCase))).ToArray();
        FilteredSigns.Clear();
        foreach (var item in found) FilteredSigns.Add(item);
        SelectedSign = found.FirstOrDefault(s => s.Code == selectedCode) ?? found.FirstOrDefault();
    }
}
