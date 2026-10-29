using System.Text.RegularExpressions;
using Evakuator.Domain.Entities;
using Evakuator.Norms.Models;
using Evakuator.Validation.Abstractions;

namespace Evakuator.Validation.Rules;

public sealed class R07_MandatoryTextAndHeaderRule : INormRule
{
    public string Id => "R07";
    public static bool ContainsPhone(string? text, string phone) => text is not null && Regex.IsMatch(text, $@"(?<!\d){Regex.Escape(phone)}(?!\d)");
    public static bool HasApproval(PlanHeader? h) => h?.Approval?.Contains("УТВЕРЖДАЮ", StringComparison.OrdinalIgnoreCase) == true;
    public static bool HasTitle(PlanHeader? h) => h?.Title?.Contains("ПЛАН ЭВАКУАЦИИ", StringComparison.OrdinalIgnoreCase) == true;
    public static bool HasInstructions(string? text) => !string.IsNullOrWhiteSpace(text) &&
        text.Contains("пожар", StringComparison.OrdinalIgnoreCase) && ContainsPhone(text, "101") && ContainsPhone(text, "112");

    public IEnumerable<ValidationIssue> Evaluate(EvacuationPlan plan, NormDatabaseDto database)
    {
        var missing = new List<string>();
        if (!HasApproval(plan.Header)) missing.Add("штамп «УТВЕРЖДАЮ»");
        if (!HasTitle(plan.Header)) missing.Add("заголовок плана");
        if (!HasInstructions(plan.FireInstructions)) missing.Add("памятка о пожаре с номерами 101 и 112");
        if (missing.Count > 0) yield return new(Id, plan.Id, IssueSeverity.Error, "Отсутствует или неполно: " + string.Join(", ", missing) + ".", true);
    }
}
