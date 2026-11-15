namespace BudgetSummaryFinal.Domain;

public sealed class ValidatedBudgetConfiguration
{
    private ValidatedBudgetConfiguration(string policyName, Func<BudgetRecord, decimal> policy, decimal minimum)
    { PolicyName = policyName; AllowedAmountPolicy = policy; MinimumPlannedAmount = minimum; }
    public string PolicyName { get; }
    public Func<BudgetRecord, decimal> AllowedAmountPolicy { get; }
    public decimal MinimumPlannedAmount { get; }
    internal static ValidatedBudgetConfiguration Create(string name, Func<BudgetRecord, decimal> policy, decimal minimum) => new(name, policy, minimum);
}

public abstract record ConfigurationResult;
public sealed record ConfigurationCreated(ValidatedBudgetConfiguration Configuration) : ConfigurationResult;
public sealed record ConfigurationRejected(IReadOnlyList<string> Errors) : ConfigurationResult;

public static class BudgetConfigurationFactory
{
    public const decimal LowestAllowedMinimum = 100m;
    public const decimal HighestAllowedMinimum = 10_000m;
    public static ConfigurationResult Create(string? policyName, decimal minimum)
    {
        var errors = new List<string>();
        if (minimum < LowestAllowedMinimum || minimum > HighestAllowedMinimum) errors.Add($"Minimum planned amount must be between {LowestAllowedMinimum} and {HighestAllowedMinimum}.");
        if (!BudgetPolicies.TryGet(policyName, out var policy)) errors.Add("Policy must be 'standard' or 'strict'.");
        return errors.Count switch
        {
            0 => new ConfigurationCreated(ValidatedBudgetConfiguration.Create(policyName!.Trim().ToLowerInvariant(), policy!, minimum)),
            _ => new ConfigurationRejected(Array.AsReadOnly(errors.ToArray()))
        };
    }

    public static Result<ValidatedBudgetConfiguration> CreateResult(string? policyName, decimal minimum) => Create(policyName, minimum) switch
    {
        ConfigurationCreated created => Result.Ok(created.Configuration),
        ConfigurationRejected rejected when minimum < LowestAllowedMinimum || minimum > HighestAllowedMinimum =>
            Result.Fail<ValidatedBudgetConfiguration>("InvalidMinimum", rejected.Errors[0]),
        ConfigurationRejected rejected =>
            Result.Fail<ValidatedBudgetConfiguration>("UnknownPolicy", rejected.Errors[0]),
        _ => throw new InvalidOperationException("Unknown configuration result.")
    };
}

public abstract record BudgetSummaryState;
public sealed record UnconfiguredBudgetSummary(IReadOnlyList<string> Reasons) : BudgetSummaryState;
public sealed record ReadyToProduceSummary(ValidatedBudgetConfiguration Configuration) : BudgetSummaryState;
public sealed record SummaryProduced(BudgetSummary Summary, ValidatedBudgetConfiguration Configuration) : BudgetSummaryState;
public abstract record SummaryTransitionResult;
public sealed record SummaryCreated(SummaryProduced ProducedState) : SummaryTransitionResult;
public sealed record SummaryTransitionRejected(string Reason, BudgetSummaryState OriginalState) : SummaryTransitionResult;

public static class BudgetSummaryWorkflow
{
    public static BudgetSummaryState Prepare(ConfigurationResult result) => result switch
    {
        ConfigurationCreated created => new ReadyToProduceSummary(created.Configuration),
        ConfigurationRejected rejected => new UnconfiguredBudgetSummary(rejected.Errors),
        _ => throw new ArgumentOutOfRangeException(nameof(result), "Unknown configuration result.")
    };

    public static SummaryTransitionResult Produce(BudgetSummaryState state, IEnumerable<BudgetRecord> records) => state switch
    {
        ReadyToProduceSummary ready => new SummaryCreated(new SummaryProduced(BudgetPipeline.ProduceSummary(records, ready.Configuration.AllowedAmountPolicy, ready.Configuration.MinimumPlannedAmount), ready.Configuration)),
        UnconfiguredBudgetSummary unconfigured => new SummaryTransitionRejected($"Cannot produce a summary before configuration succeeds: {string.Join(" ", unconfigured.Reasons)}", unconfigured),
        SummaryProduced produced => new SummaryTransitionRejected("This summary was already produced. Repeated production is rejected.", produced),
        _ => throw new ArgumentOutOfRangeException(nameof(state), "Unknown workflow state.")
    };
}
