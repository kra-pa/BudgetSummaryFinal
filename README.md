# Budget Summary Final - Assignments 5-8

Student: Parunina Kira  
Group: IT-2505  
Project type: Individual project

## Purpose and normal workflow

This console program helps a department manager review budget records from `data/budget-records.csv`. It validates the configuration, loads immutable records, materialises the eligible-record snapshot, produces the existing pure summary pipeline, looks up the requested record, calculates a native utilization percentage, and displays two reports.

Input columns: `Id,Category,PlannedAmount,ActualAmount,IsApproved`. Amounts are non-negative. Summary inclusion requires approval and planned amount at least the configured minimum (100-10,000). `standard` allows 10% over plan; `strict` allows no excess.

```powershell
# From this folder; .NET SDK 8.0+ and UCRT64 GCC are required only to rebuild the DLL.
dotnet build
dotnet run --project .\BudgetSummaryFinal.csproj
dotnet run --project .\BudgetSummaryFinal.csproj -- --policy strict --minimum 100 --record B005
dotnet run --project .\BudgetSummaryFinal.csproj -- --record UNKNOWN
dotnet run --project .\BudgetSummaryFinal.csproj -- .\missing.csv
dotnet run --project .\BudgetSummaryFinal.csproj -- --export .\output\category-report.csv
dotnet run --project .\BudgetSummaryFinal.Tests\BudgetSummaryFinal.Tests.csproj
```

Expected normal result: standard policy includes B001, B002, B004 and has one over-budget item; strict policy has three. The focused B005 record is found even though `IsApproved=false`, demonstrating that false is valid data rather than missing data.

## Error handling

`Result<T>` has mutually exclusive `Success<T>` and `Failure<T>` cases. Constructors reject missing payloads. `DomainError` carries a code and useful message. `Option<T>` uses `Some` and `None` for record lookup without relying on `default(T)`.

`BudgetApplication.Run` uses this active Result chain:

`LoadRecords → Bind(configuration) → Map(PreparedInput) → Bind(produce) → Map(reports) → Bind(native metric) → Bind(lookup) → Map(final dashboard)`.

Map transforms a success; Bind invokes a later Result-returning step only after success. A failure skips all dependent steps. The final console boundary pattern-matches only success or failure.

| Error code | Meaning |
| --- | --- |
| `InvalidMinimum` | Minimum is not a number or outside 100-10,000. |
| `UnknownPolicy` | Policy is not `standard` or `strict`. |
| `RecordNotFound` | Requested `--record` ID is absent. |
| `MalformedCsv` / `FileNotFound` | Input is invalid or cannot be opened. |
| `NativeInputInvalid` / `NativeLibraryUnavailable` | Native metric cannot use the values or DLL. |

`BudgetDataBoundary.LoadRecords` owns a `StreamReader` with `using`; it closes it after both successful and malformed reads. Missing/invalid file handling remains at the boundary, separate from pure domain functions.

`BudgetDataBoundary.ExportCategoryReport` is the corresponding output boundary. It creates the requested relative or absolute output folder, owns a `StreamWriter` with `using`, and exports the category report only after a successful workflow. It returns `ExportAccessDenied` or `ExportFailed` instead of hiding operational output errors.

### Custom extensions

1. `WhereApprovedAtLeast(this IEnumerable<BudgetRecord>, decimal)` is a reusable budget-domain selection. It belongs on a record sequence because it filters according to the project’s inclusion rule. It is used by the pipeline and variance report.
2. `ToStableSnapshot<T>(this IEnumerable<T>)` materialises an immutable, reusable `IReadOnlyList<T>`. It belongs on an enumerable because it makes the ownership and materialisation boundary explicit. It is used by the pipeline, reports, and lazy workflow.

### LINQ reports

1. **Over-budget variance report**: for the manager to find approved qualifying records over their policy limit. Input: actual CSV records and selected policy. It uses `Where`, `Select`, another `Where`, `OrderByDescending` risk score, and `ThenBy` ID. Output: ID, category, actual, allowed, risk score. Standard sample: B002 only; strict sample: B002, B001, B004.
2. **Category spending report**: for comparing actual spending by all CSV categories, including unapproved and below-threshold entries. It uses `GroupBy`, `Count`, `Sum`, `OrderByDescending` actual total, and `ThenBy` category. Output: category, record count, planned and actual total. On the sample, Equipment is first with 1,050 actual.

Both reports are pure methods in `BudgetReports`; console display happens afterwards. Tests cover normal result, empty input, no matches, tied primary values, deterministic tie-breaks, grouping, and totals.

### Currying and partial application

`BudgetRiskScoring.CreateRiskScore(decimal tolerance)` is curried: supplying tolerance returns a function from `BudgetRecord` to risk score. The application creates and passes two partial applications into the variance report:

* `StandardRiskScore = CreateRiskScore(0.10m)`;
* `StrictRiskScore = CreateRiskScore(0m)`.

For an actual amount 110 and plan 100, standard score is 0 while strict score is 10. No global mutable setting changes when policy changes.

### Lazy processing and resources

`BudgetLazyWorkflow.StreamEligibleRecords` uses `yield return`; constructing its `IEnumerable` does not enumerate its source. `BudgetApplication` deliberately calls `MaterializeEligibleSnapshot` before the safe state transition, creating a stable snapshot used by the summary. Before materialisation, every enumeration would re-run filtering and may re-enumerate the source; after it, repeated reads of the snapshot do neither.

CSV I/O is fully read inside the `using StreamReader` lifetime, then converted to immutable records. Therefore no deferred workflow can access a disposed reader. No cache, event subscription, or long-lived closure is used. The materialised snapshot costs one allocation but avoids repeated filtering and prevents retaining file resources; this is appropriate for the small budget dataset. Streaming would be preferable for a much larger one-pass report, but must keep the reader alive throughout enumeration.

## Native C integration (Windows x64)

`native/budget_native.c` exports `int32_t budget_utilization_percent(int64_t planned_cents, int64_t actual_cents)`. Input units are integer cents; planned must be positive and actual non-negative. It returns `floor(actual_cents * 100 / planned_cents)`, or `-1` for invalid input. Example: planned 10,000 cents and actual 11,399 cents returns 113. No native memory is allocated or owned.

Build/rebuild on Windows x64 with MSYS2 UCRT64 GCC:

```powershell
.\native\build-native.ps1
```

The command runs `gcc -shared -O2 -o native\budget_native.dll native\budget_native.c`. The compiled DLL is committed in `native/` and MSBuild copies it to the application output root, where `DllImport("budget_native.dll")` resolves it. `NativeBudgetMetrics` validates decimal amounts, converts to 64-bit cents, calls the export with `Cdecl`, and converts loading/binding problems into Result failures. Its result appears as `Native utilization: 108%` in the dashboard.

Integration difficulty resolved: MSBuild initially placed the DLL under an output `native/` subfolder while P/Invoke searched the output root. `TargetPath` in the main `.csproj` now copies it as `budget_native.dll` directly beside the executable.

## Requirement map and tests

| Requirement | Implementation |
| --- | --- |
| Pure pipeline / safe states / Result and Option | `Domain/BudgetPipeline.cs`, `BudgetSummaryWorkflow.cs`, `Result.cs`, `Option.cs` |
| Both custom extensions | `Domain/BudgetExtensions.cs` |
| LINQ reports and currying | `Domain/BudgetReports.cs` |
| Deferred `yield return` and snapshot | `BudgetLazyWorkflow` in `Domain/BudgetReports.cs` |
| File resource ownership | `BudgetDataBoundary.LoadRecords` in `Domain/BudgetApplication.cs` |
| Native C source, binding, adapter | `native/`, `Domain/NativeBudgetMetrics.cs` |
| Integrated console workflow | `Domain/BudgetApplication.cs`, `Program.cs`, `BudgetConsolePresenter.cs` |
| Tests | `BudgetSummaryFinal.Tests/Program.cs` |

The assertions runner has 21 cumulative tests: at least 8 pure/validation tests, 2 complete workflows, 2 file tests, extensions, reports, currying, deferred timing/materialisation, resource release, export, real native call, and controlled native failure. Latest verified outcome: `21/21 tests passed`.

## Git submission record

Repository URL: https://github.com/kra-pa/BudgetSummaryFinal 
Branch: `main`  
Final commit: e3f2e3aac06ca1b6a32107c8f0ad3dc2696c2ed7

The Moodle ZIP must contain the same source version as this commit. Do not include `bin`, `obj`, `.vs`, IDE caches, credentials, or generated export files; `.gitignore` is provided for this purpose.

