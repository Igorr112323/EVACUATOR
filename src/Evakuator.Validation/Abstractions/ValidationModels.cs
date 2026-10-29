using System.Collections.Immutable;
using Evakuator.Domain.Entities;
using Evakuator.Norms.Models;

namespace Evakuator.Validation.Abstractions;

public enum IssueSeverity { Error, Warning }

public sealed record ValidationIssue(string RuleId, string ObjectId, IssueSeverity Severity, string Message,
    bool CanAutoFix = false, string? RelatedObjectId = null, bool RequiresHumanReview = false,
    string Reference = "[ПРОВЕРИТЬ ПО ТЕКСТУ ГОСТ]");

public sealed record ValidationReport(ImmutableArray<ValidationIssue> Issues, int RuleCount, double ComplianceScore)
{
    public int ErrorCount => Issues.Count(i => i.Severity == IssueSeverity.Error);
    public int WarningCount => Issues.Count(i => i.Severity == IssueSeverity.Warning);
    public string Scope => "Доля пройденных учебных правил MVP; не сертификат соответствия ГОСТ и не проверка безопасности маршрута.";
}

public sealed record FixDiffEntry(string RuleId, string ObjectId, string Field, string Before, string After,
    bool RequiresHumanReview, string Reason);

public sealed record AutoFixResult(EvacuationPlan Plan, ValidationReport Before, ValidationReport After,
    ImmutableArray<FixDiffEntry> Changes, ImmutableArray<string> ManualActions);

public interface INormRule
{
    string Id { get; }
    IEnumerable<ValidationIssue> Evaluate(EvacuationPlan plan, NormDatabaseDto database);
}
