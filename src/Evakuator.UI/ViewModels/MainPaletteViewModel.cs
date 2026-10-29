using System.Collections.Immutable;
using System.Globalization;
using Evakuator.CadAdapter.Commands;
using Evakuator.CadAdapter.Contracts;
using Evakuator.Domain.Entities;
using Evakuator.Domain.ValueObjects;
using Evakuator.Norms.Models;
using Evakuator.Norms.Services;
using Evakuator.Validation.Abstractions;

namespace Evakuator.UI.ViewModels;

public sealed class MainPaletteViewModel : ObservableObject, IDisposable
{
    private readonly ICadDocumentContext context;
    private readonly Func<NormDatabaseDto> database;
    private readonly EvacCommands commands;
    private readonly Action<Action> dispatch;
    private readonly HotReloadNormWatcher? watcher;
    private EvacuationPlan plan;
    private NormDatabaseDto currentDatabase;
    private IReadOnlySet<string> highlightedIds = ImmutableHashSet<string>.Empty;
    private string feedback = "Учебный режим · требуется проверка проектировщиком";
    private string scaleInput = "100";
    private string heightInput = "10";
    private bool disposed;

    public SignCatalogViewModel Catalog { get; }
    public NormInspectorViewModel Inspector { get; }
    public LegendPreviewViewModel Legend { get; } = new();
    public RelayCommand InsertSignCommand { get; }
    public EvacuationPlan CurrentPlan { get => plan; private set { if (Set(ref plan, value)) { Raise(nameof(PlanName)); Raise(nameof(SignCount)); Raise(nameof(PathCount)); } } }
    public NormDatabaseDto Database { get => currentDatabase; private set => Set(ref currentDatabase, value); }
    public IReadOnlySet<string> HighlightedIds { get => highlightedIds; private set => Set(ref highlightedIds, value); }
    public string PlanName => CurrentPlan.Name;
    public int SignCount => CurrentPlan.Signs.Length;
    public int PathCount => CurrentPlan.Paths.Length;
    public string Feedback { get => feedback; private set => Set(ref feedback, value); }
    public string ScaleInput { get => scaleInput; set => Set(ref scaleInput, value); }
    public string HeightInput { get => heightInput; set => Set(ref heightInput, value); }

    public MainPaletteViewModel(ICadDocumentContext context, Func<NormDatabaseDto> database,
        Action<Action>? dispatch = null, HotReloadNormWatcher? watcher = null)
    {
        this.context = context;
        this.database = database;
        this.dispatch = dispatch ?? (action => action());
        this.watcher = watcher;
        plan = context.Read().Plan;
        currentDatabase = database();
        commands = new EvacCommands(context, database);
        Catalog = new SignCatalogViewModel(currentDatabase);
        Inspector = new NormInspectorViewModel(context, database, commands);
        Inspector.ReportChanged += OnReport;
        Inspector.StatusChanged += OnInspectorStatusChanged;
        InsertSignCommand = new RelayCommand(InsertSign);
        context.Changed += OnDocumentChanged;
        if (watcher is not null) { watcher.Reloaded += OnNormsReloaded; watcher.ReloadFailed += OnNormsFailed; }
        Refresh();
    }

    private void OnDocumentChanged(CadSnapshot _) => dispatch(() => { if (!disposed) Refresh(); });
    private void OnNormsReloaded(NormDatabaseDto _) => dispatch(() =>
    {
        if (disposed) return;
        Database = database();
        Catalog.UpdateDatabase(Database);
        commands.RebuildLegend();
        Feedback = "Нормбаза обновлена; все параметры требуют официальной сверки.";
        Refresh();
    });
    private void OnNormsFailed(Exception ex) => dispatch(() => { if (!disposed) Feedback = "Ошибка нормбазы; сохранена предыдущая версия: " + ex.Message; });
    private void OnInspectorStatusChanged(string status) => Feedback = status;
    private void OnReport(ValidationReport report) => HighlightedIds = report.Issues.Where(i => i.Severity == IssueSeverity.Error)
        .SelectMany(i => i.RelatedObjectId is null ? new[] { i.ObjectId } : new[] { i.ObjectId, i.RelatedObjectId })
        .ToImmutableHashSet(StringComparer.Ordinal);

    public void Refresh()
    {
        CurrentPlan = context.Read().Plan;
        Database = database();
        Legend.Refresh(CurrentPlan, Database);
        Inspector.Refresh();
    }

    private void InsertSign()
    {
        var selected = Catalog.SelectedSign;
        if (selected is null) { Feedback = "Выберите знак в каталоге."; return; }
        static bool Parse(string input, out double value) => double.TryParse(input, NumberStyles.Float, CultureInfo.CurrentCulture, out value) ||
            double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        if (!Parse(HeightInput, out var height) || !Parse(ScaleInput, out var scale) || height <= 0 || scale <= 0)
        { Feedback = "Введите положительные размер и знаменатель масштаба."; return; }
        try
        {
            var p = context.Read().Plan;
            commands.InsertSign(selected.Code, new Point2D(p.Width / 2, p.Height / 2), height, scale, needsReview: true);
            Feedback = $"{selected.Code} добавлен в центр. Обязательно проверьте его положение.";
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        { Feedback = "Не удалось добавить знак: " + ex.Message; }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        context.Changed -= OnDocumentChanged;
        Inspector.ReportChanged -= OnReport;
        Inspector.StatusChanged -= OnInspectorStatusChanged;
        if (watcher is not null) { watcher.Reloaded -= OnNormsReloaded; watcher.ReloadFailed -= OnNormsFailed; }
    }
}
