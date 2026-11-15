namespace BudgetSummaryFinal.Domain;

public sealed record BudgetApplicationReport(
    BudgetSummary Summary,
    BudgetRecord FocusRecord,
    IReadOnlyList<VarianceReportEntry> OverBudgetReport,
    IReadOnlyList<CategorySpendingTotal> CategorySpendingReport,
    int NativeUtilizationPercent);

public sealed record ReportExportReceipt(string Path, int CategoryCount);

public static class BudgetRecordLookup
{
    public static Option<BudgetRecord> FindById(IEnumerable<BudgetRecord> records, string? id)
    {
        foreach (var record in records)
            if (string.Equals(record.Id, id, StringComparison.OrdinalIgnoreCase))
                return new Some<BudgetRecord>(record);

        return new None<BudgetRecord>();
    }
}

public static class BudgetDataBoundary
{
    public static Result<IReadOnlyList<BudgetRecord>> LoadRecords(string path)
    {
        try
        {
            using var reader = new StreamReader(path);
            return BudgetCsvParser.Parse(reader.ReadToEnd());
        }
        catch (FileNotFoundException)
        {
            return Result.Fail<IReadOnlyList<BudgetRecord>>("FileNotFound", $"Budget data file was not found: '{path}'.");
        }
        catch (DirectoryNotFoundException)
        {
            return Result.Fail<IReadOnlyList<BudgetRecord>>("FileNotFound", $"Budget data directory was not found for: '{path}'.");
        }
        catch (UnauthorizedAccessException exception)
        {
            return Result.Fail<IReadOnlyList<BudgetRecord>>("FileAccessDenied", $"Cannot read '{path}': {exception.Message}");
        }
        catch (IOException exception)
        {
            return Result.Fail<IReadOnlyList<BudgetRecord>>("FileReadFailed", $"Cannot read '{path}': {exception.Message}");
        }
        catch (FormatException exception)
        {
            return Result.Fail<IReadOnlyList<BudgetRecord>>("MalformedCsv", $"Cannot parse '{path}': {exception.Message}");
        }
    }

    public static Result<ReportExportReceipt> ExportCategoryReport(string path, IEnumerable<CategorySpendingTotal> totals)
    {
        try
        {
            var snapshot = totals.ToStableSnapshot();
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (directory is not null) Directory.CreateDirectory(directory);

            using var writer = new StreamWriter(path, false);
            writer.WriteLine("Category,RecordCount,TotalPlanned,TotalActual");
            foreach (var total in snapshot)
                writer.WriteLine($"{EscapeCsv(total.Category)},{total.RecordCount},{total.TotalPlanned.ToString(System.Globalization.CultureInfo.InvariantCulture)},{total.TotalActual.ToString(System.Globalization.CultureInfo.InvariantCulture)}");

            return Result.Ok(new ReportExportReceipt(path, snapshot.Count));
        }
        catch (UnauthorizedAccessException exception)
        {
            return Result.Fail<ReportExportReceipt>("ExportAccessDenied", $"Cannot export '{path}': {exception.Message}");
        }
        catch (IOException exception)
        {
            return Result.Fail<ReportExportReceipt>("ExportFailed", $"Cannot export '{path}': {exception.Message}");
        }
    }

    private static string EscapeCsv(string value) => value.Contains(',') || value.Contains('"')
        ? $"\"{value.Replace("\"", "\"\"")}\""
        : value;
}

public static class BudgetApplication
{
    public static Result<BudgetApplicationReport> Run(string path, string? policyName, decimal minimum, string? focusRecordId, INativeBudgetMetrics? nativeMetrics = null) =>
        BudgetDataBoundary.LoadRecords(path)
            .Bind(records => BudgetConfigurationFactory.CreateResult(policyName, minimum)
                .Map(configuration => CreatePreparedInput(records, configuration)))
            .Bind(prepared => Produce(prepared)
                .Map(summary => CreateProducedInput(prepared, summary)))
            .Bind(produced => (nativeMetrics ?? new NativeBudgetMetrics())
                .CalculateUtilizationPercent(produced.Summary.TotalPlanned, produced.Summary.TotalActual)
                .Map(utilization => new EnrichedInput(produced, utilization)))
            .Bind(enriched => BudgetRecordLookup.FindById(enriched.Produced.Records, focusRecordId)
                .ToResult(new DomainError("RecordNotFound", $"Budget record '{focusRecordId}' was not found."))
                .Map(record => new BudgetApplicationReport(
                    enriched.Produced.Summary,
                    record,
                    enriched.Produced.OverBudgetReport,
                    enriched.Produced.CategorySpendingReport,
                    enriched.NativeUtilizationPercent)));

    private static PreparedInput CreatePreparedInput(IReadOnlyList<BudgetRecord> records, ValidatedBudgetConfiguration configuration)
    {
        var stableRecords = records.ToStableSnapshot();
        var eligibleSnapshot = BudgetLazyWorkflow.MaterializeEligibleSnapshot(stableRecords, configuration.MinimumPlannedAmount);
        return new PreparedInput(stableRecords, eligibleSnapshot, configuration);
    }

    private static ProducedInput CreateProducedInput(PreparedInput prepared, BudgetSummary summary)
    {
        var riskScore = prepared.Configuration.PolicyName == "strict" ? BudgetRiskScoring.StrictRiskScore : BudgetRiskScoring.StandardRiskScore;
        return new ProducedInput(
            prepared.Records,
            summary,
            BudgetReports.CreateOverBudgetReport(prepared.Records, prepared.Configuration.MinimumPlannedAmount, prepared.Configuration.AllowedAmountPolicy, riskScore),
            BudgetReports.CreateCategorySpendingReport(prepared.Records));
    }

    private static Result<BudgetSummary> Produce(PreparedInput input) => BudgetSummaryWorkflow.Produce(
        new ReadyToProduceSummary(input.Configuration), input.EligibleSnapshot) switch
    {
        SummaryCreated created => Result.Ok(created.ProducedState.Summary),
        SummaryTransitionRejected rejected => Result.Fail<BudgetSummary>("OperationRejected", rejected.Reason),
        _ => throw new InvalidOperationException("Unknown workflow transition.")
    };

    private sealed record PreparedInput(IReadOnlyList<BudgetRecord> Records, IReadOnlyList<BudgetRecord> EligibleSnapshot, ValidatedBudgetConfiguration Configuration);
    private sealed record ProducedInput(IReadOnlyList<BudgetRecord> Records, BudgetSummary Summary, IReadOnlyList<VarianceReportEntry> OverBudgetReport, IReadOnlyList<CategorySpendingTotal> CategorySpendingReport);
    private sealed record EnrichedInput(ProducedInput Produced, int NativeUtilizationPercent);
}
