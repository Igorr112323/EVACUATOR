using System.Collections.ObjectModel;
using Evakuator.CadAdapter.Commands;
using Evakuator.CadAdapter.Contracts;
using Evakuator.Norms.Models;
using Evakuator.Validation.Abstractions;
using Evakuator.Validation.Engine;

namespace Evakuator.UI.ViewModels;

public sealed record IssueViewModel(string RuleId, string ObjectId, string Message, string SeverityLabel, bool IsError, string Reference);

public sealed class NormInspectorViewModel : ObservableObject
{
    private readonly ICadDocumentContext context;
    private readonly Func<NormDatabaseDto> database;
    private readonly EvacCommands commands;
    private ValidationReport? report;
    private string status = "Проверка учебных правил";
    public ObservableCollection<IssueViewModel> Issues { get; } = [];
    public ObservableCollection<string> ManualActions { get; } = [];
    public event Action<ValidationReport>? ReportChanged;
    public event Action<string>? StatusChanged;
    public ValidationReport? Report { get => report; private set { if (Set(ref report, value)) { Raise(nameof(ScoreText)); Raise(nameof(ErrorCount)); Raise(nameof(WarningCount)); } } }
    public string ScoreText => Report is null ? "—" : $"{Report.ComplianceScore:0.##}%";
    public int ErrorCount => Report?.ErrorCount ?? 0;
    public int WarningCount => Report?.WarningCount ?? 0;
    public string Status
    {
        get => status;
        private set { Set(ref status, value); StatusChanged?.Invoke(value); }
    }
    public RelayCommand CheckCommand { get; }
    public RelayCommand HealCommand { get; }
    public RelayCommand UndoCommand { get; }

    public NormInspectorViewModel(ICadDocumentContext context, Func<NormDatabaseDto> database, EvacCommands commands)
    {
        this.context = context;
        this.database = database;
        this.commands = commands;
        CheckCommand = new RelayCommand(Refresh);
        HealCommand = new RelayCommand(Heal);
        UndoCommand = new RelayCommand(Undo);
        Refresh();
    }

    public void Refresh()
    {
        Report = new NormController().Validate(context.Read().Plan, database());
        Issues.Clear();
        foreach (var issue in Report.Issues)
            Issues.Add(new IssueViewModel(issue.RuleId, issue.ObjectId, issue.Message,
                issue.Severity == IssueSeverity.Error ? "ОШИБКА" : "ПРОВЕРИТЬ", issue.Severity == IssueSeverity.Error, issue.Reference));
        ReportChanged?.Invoke(Report);
    }

    private void Heal()
    {
        try
        {
            var result = commands.FixAll();
            ManualActions.Clear();
            foreach (var action in result.ManualActions) ManualActions.Add(action);
            Status = $"Изменений: {result.Changes.Length}. Ручная проверка: {result.ManualActions.Length}.";
            Refresh();
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        { Status = "Не удалось исправить: " + ex.Message; }
    }

    private void Undo()
    {
        Status = context.Undo() ? "Последнее изменение отменено." : "Нечего отменять.";
        ManualActions.Clear();
        Refresh();
    }
}
