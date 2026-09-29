namespace BrainSharp.NugetCheck.ConsoleApp;

public static class ExitCodes
{
    public const int Success = 0;
    public const int WarningsFound = 1;
    public const int InvalidUsage = 2;
    public const int ScanFailed = 3;

    public static int FromWarningCount(int warningCount) => warningCount == 0 ? Success : WarningsFound;

    // an incomplete scan must never look clean, so failures win over warnings
    public static int FromScan(int warningCount, int failedCount) => failedCount > 0 ? ScanFailed : FromWarningCount(warningCount);
}
