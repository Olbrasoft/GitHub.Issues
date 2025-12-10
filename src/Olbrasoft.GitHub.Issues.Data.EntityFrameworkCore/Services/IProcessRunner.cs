namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Services;

/// <summary>
/// Abstraction for running external processes.
/// Enables unit testing by allowing mock implementations.
/// </summary>
public interface IProcessRunner
{
    /// <summary>
    /// Runs an external process and waits for it to complete.
    /// </summary>
    /// <param name="fileName">The name of the executable to run.</param>
    /// <param name="arguments">Command line arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing exit code and output.</returns>
    Task<ProcessResult> RunAsync(string fileName, string arguments, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of running an external process.
/// </summary>
public record ProcessResult(int ExitCode, string StandardOutput, string StandardError)
{
    /// <summary>
    /// Returns true if the process exited with code 0.
    /// </summary>
    public bool Success => ExitCode == 0;
}
