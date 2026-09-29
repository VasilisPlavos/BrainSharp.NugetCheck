namespace BrainSharp.NugetCheck.ConsoleApp;

/// <summary>Shows progress on one line that overwrites itself.</summary>
public sealed class ConsoleProgress : IProgress<string>
{
    public void Report(string value)
    {
        // keep piped output and CI logs clean
        if (Console.IsOutputRedirected) return;

        try
        {
            var width = Math.Max(Console.WindowWidth - 1, 1);
            Console.Write('\r' + (value.Length > width ? value[..width] : value.PadRight(width)));
        }
        catch (IOException)
        {
            // no real console window (e.g. some IDE terminals)
        }
    }
}
