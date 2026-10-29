using System.Collections.Immutable;
using Evakuator.Domain.Entities;
using Evakuator.Domain.Serialization;
using Evakuator.Norms.Models;
using Evakuator.Validation.Abstractions;
using Evakuator.Validation.Rules;

namespace Evakuator.Validation.Engine;

public sealed class NormController
{
    private readonly ImmutableArray<INormRule> rules;
    public NormController() : this([new R01_SignSizeBoundsRule(), new R02_UniformSignScaleRule(),
        new R03_EvacuationPathIntegrityRule(), new R04_YouAreHerePresenceRule(), new R05_LegendSyncRule(),
        new R06_SignCollisionAndOverlapRule(), new R07_MandatoryTextAndHeaderRule()]) { }
    public NormController(IEnumerable<INormRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        this.rules = rules.ToImmutableArray();
        if (this.rules.IsEmpty || this.rules.Any(r => r is null || string.IsNullOrWhiteSpace(r.Id)) || this.rules.Select(r => r.Id).Distinct(StringComparer.Ordinal).Count() != this.rules.Length)
            throw new ArgumentException("Rules must be non-empty and have unique ids.", nameof(rules));
    }
    public ValidationReport Validate(EvacuationPlan plan, NormDatabaseDto database)
    {
        PlanJson.CheckStructure(plan);
        ArgumentNullException.ThrowIfNull(database);
        var issues = rules.SelectMany(r => r.Evaluate(plan, database).Select(issue =>
            issue.RuleId == r.Id ? issue : throw new InvalidOperationException($"Rule {r.Id} returned an issue for {issue.RuleId}."))).ToImmutableArray();
        var failedRules = issues.Where(i => i.Severity == IssueSeverity.Error).Select(i => i.RuleId).Distinct(StringComparer.Ordinal).Count();
        var score = Math.Round(100d * (rules.Length - failedRules) / rules.Length, 2, MidpointRounding.AwayFromZero);
        return new ValidationReport(issues, rules.Length, score);
    }
}
