using Evakuator.CadAdapter.Commands;
using Evakuator.CadAdapter.Simulation;
using Evakuator.Domain.Enums;
using Evakuator.UI.ViewModels;

namespace Evakuator.Tests;

public class UiViewModelTests
{
    [Fact]
    public void CatalogSearchAndCategoryFilterWorkTogether()
    {
        var catalog = new SignCatalogViewModel(TestData.Database());
        Assert.Equal(30, catalog.FilteredSigns.Count);
        catalog.Search = "огнетуш";
        Assert.Contains(catalog.FilteredSigns, item => item.Code == "F04");
        catalog.SelectedCategory = SignCategory.E.ToString();
        Assert.Empty(catalog.FilteredSigns);
        catalog.SelectedCategory = SignCategory.F.ToString();
        Assert.Contains(catalog.FilteredSigns, item => item.Code == "F04");
        catalog.Search = "f10";
        Assert.Single(catalog.FilteredSigns);
        Assert.Equal("F10", catalog.FilteredSigns[0].Code);
    }

    [Fact]
    public void CatalogReloadPreservesSelectionAndUpdatesPreview()
    {
        var db = TestData.Database();
        var catalog = new SignCatalogViewModel(db);
        catalog.SelectedSign = catalog.FilteredSigns.Single(s => s.Code == "F04");
        var oldPreview = catalog.SelectedSign!.Preview;
        var index = Array.FindIndex(db.Signs.ToArray(), s => s.Code == "F04");
        var update = db with { Signs = db.Signs.SetItem(index, db.Signs[index] with { Name = "Изменённый огнетушитель", BackgroundColor = "#FF00FF" }) };
        catalog.UpdateDatabase(update);
        Assert.Equal("F04", catalog.SelectedSign!.Code);
        Assert.Equal("Изменённый огнетушитель", catalog.SelectedSign.Name);
        Assert.NotEqual(oldPreview, catalog.SelectedSign!.Preview);
        Assert.Contains("ПРОВЕРИТЬ", catalog.SelectedSign!.Reference);
    }

    [Fact]
    public void InspectorCanValidateHealAndShowManualActions()
    {
        var plan = TestData.CleanPlan() with { Legend = null, Markers = [] };
        var context = new InMemoryCadDocumentContext(plan);
        var commands = new EvacCommands(context, TestData.Database);
        var inspector = new NormInspectorViewModel(context, TestData.Database, commands);
        Assert.Contains(inspector.Issues, i => i.RuleId == "R04");
        Assert.Contains(inspector.Issues, i => i.RuleId == "R05");
        inspector.HealCommand.Execute(null);
        Assert.DoesNotContain(inspector.Issues, i => i.IsError);
        Assert.Contains(inspector.Issues, i => !i.IsError);
        Assert.NotEmpty(inspector.ManualActions);
        Assert.Contains("не сертификат", inspector.Report!.Scope, StringComparison.OrdinalIgnoreCase);
        Assert.True(context.Undo());
        inspector.Refresh();
        Assert.Contains(inspector.Issues, i => i.RuleId == "R05" && i.IsError);
    }

    [Fact]
    public void MainPaletteInsertionAndRepeatedUndoNeverLeaveStaleFeedback()
    {
        var db = TestData.Database();
        var context = new InMemoryCadDocumentContext(TestData.CleanPlan());
        using var main = new MainPaletteViewModel(context, () => db);
        main.Catalog.SelectedSign = main.Catalog.FilteredSigns.Single(s => s.Code == "F04");
        main.InsertSignCommand.Execute(null);
        Assert.Equal(3, main.SignCount);
        Assert.Contains("добавлен", main.Feedback);
        main.Inspector.UndoCommand.Execute(null);
        Assert.Equal(2, main.SignCount);
        Assert.Contains("отменено", main.Feedback);
        // The same undo status can be emitted twice with an insertion in between.
        main.InsertSignCommand.Execute(null);
        main.Inspector.UndoCommand.Execute(null);
        Assert.Equal(2, main.SignCount);
        Assert.Contains("отменено", main.Feedback);
    }

    [Fact]
    public void CatalogGracefullyHandlesEmptySearchResults()
    {
        var catalog = new SignCatalogViewModel(TestData.Database());
        catalog.Search = "не существующий код 987654";
        Assert.Empty(catalog.FilteredSigns);
        Assert.Null(catalog.SelectedSign);
    }
}
