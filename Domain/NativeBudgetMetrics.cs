using System.Runtime.InteropServices;

namespace BudgetSummaryFinal.Domain;

public interface INativeBudgetMetrics
{
    Result<int> CalculateUtilizationPercent(decimal plannedAmount, decimal actualAmount);
}

public sealed class NativeBudgetMetrics : INativeBudgetMetrics
{
    public Result<int> CalculateUtilizationPercent(decimal plannedAmount, decimal actualAmount)
    {
        if (plannedAmount <= 0m || actualAmount < 0m)
            return Result.Fail<int>("NativeInputInvalid", "Native utilization requires planned amount above zero and actual amount at least zero.");

        try
        {
            var plannedCents = decimal.ToInt64(decimal.Truncate(plannedAmount * 100m));
            var actualCents = decimal.ToInt64(decimal.Truncate(actualAmount * 100m));
            var utilization = NativeMethods.BudgetUtilizationPercent(plannedCents, actualCents);
            return utilization >= 0
                ? Result.Ok(utilization)
                : Result.Fail<int>("NativeInputInvalid", "Native utilization rejected the provided monetary values.");
        }
        catch (DllNotFoundException exception)
        {
            return Result.Fail<int>("NativeLibraryUnavailable", $"budget_native.dll could not be loaded: {exception.Message}");
        }
        catch (EntryPointNotFoundException exception)
        {
            return Result.Fail<int>("NativeBindingFailed", $"Native export BudgetUtilizationPercent was not found: {exception.Message}");
        }
        catch (BadImageFormatException exception)
        {
            return Result.Fail<int>("NativeLibraryUnavailable", $"Native library architecture is incompatible: {exception.Message}");
        }
        catch (OverflowException)
        {
            return Result.Fail<int>("NativeInputInvalid", "Monetary values are outside the native 64-bit cents range.");
        }
    }

    private static class NativeMethods
    {
        [DllImport("budget_native.dll", EntryPoint = "budget_utilization_percent", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern int BudgetUtilizationPercent(long plannedCents, long actualCents);
    }
}

public sealed class FailingNativeBudgetMetrics(string message) : INativeBudgetMetrics
{
    public Result<int> CalculateUtilizationPercent(decimal plannedAmount, decimal actualAmount) =>
        Result.Fail<int>("NativeLibraryUnavailable", message);
}
