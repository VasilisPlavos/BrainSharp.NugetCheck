namespace BrainSharp.NugetCheck.Tests.Fakes;

// Progress<T> reports asynchronously; this records synchronously so tests can assert on it.
public class RecordingProgress : IProgress<string>
{
    public List<string> Messages { get; } = [];

    public void Report(string value) => Messages.Add(value);
}
