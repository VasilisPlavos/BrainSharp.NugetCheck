namespace BrainSharp.NugetCheck.ConsoleApp;

public static class ExitCodes
{
    public const int Success = 0;
    public const int WarningsFound = 1;
    public const int InvalidUsage = 2;

    public static int FromWarningCount(int warningCount) => warningCount == 0 ? Success : WarningsFound;
}
