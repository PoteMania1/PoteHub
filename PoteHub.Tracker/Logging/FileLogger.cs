namespace PoteHub.Tracker.Logging;

public class FileLogger
{
    private readonly string _logsDirectory;

    private readonly SemaphoreSlim _writeLock =
        new(1, 1);

    public FileLogger(string logsDirectory)
    {
        _logsDirectory = logsDirectory;

        Directory.CreateDirectory(
            _logsDirectory);
    }

    public async Task LogInformationAsync(
        string message)
    {
        await WriteAsync(
            "INFO",
            message);
    }

    public async Task LogErrorAsync(
        Exception exception)
    {
        string message =
            $"{exception.GetType().Name}: " +
            $"{exception.Message}" +
            Environment.NewLine +
            exception.StackTrace;

        await WriteAsync(
            "ERROR",
            message);
    }

    private async Task WriteAsync(
        string level,
        string message)
    {
        string filePath = Path.Combine(
            _logsDirectory,
            $"tracker-{DateTime.UtcNow:yyyy-MM-dd}.log");

        string entry =
            $"[{DateTime.UtcNow:O}] " +
            $"[{level}] {message}" +
            Environment.NewLine;

        await _writeLock.WaitAsync();

        try
        {
            await File.AppendAllTextAsync(
                filePath,
                entry);
        }
        finally
        {
            _writeLock.Release();
        }
    }
}