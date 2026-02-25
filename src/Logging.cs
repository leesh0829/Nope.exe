namespace NopeExe;

public interface ILogger
{
    void Info(string message);
    void Warn(string message);
    void Error(string message);
}

public sealed class CompositeLogger : ILogger
{
    private readonly IReadOnlyList<ILogSink> _sinks;

    public CompositeLogger(params ILogSink[] sinks)
    {
        _sinks = sinks;
    }

    public void Info(string message) => Log("INFO", message);
    public void Warn(string message) => Log("WARN", message);
    public void Error(string message) => Log("ERROR", message);

    private void Log(string level, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";
        foreach (var sink in _sinks)
        {
            sink.Write(line);
        }
    }
}

public interface ILogSink
{
    void Write(string message);
}

public sealed class ConsoleLogSink : ILogSink
{
    public void Write(string message) => Console.WriteLine(message);
}

public sealed class RollingFileLogSink : ILogSink
{
    private readonly string _path;
    private readonly long _maxBytes;
    private readonly int _maxFiles;
    private readonly object _lock = new();

    public RollingFileLogSink(string path, long maxBytes, int maxFiles)
    {
        _path = path;
        _maxBytes = maxBytes;
        _maxFiles = Math.Max(maxFiles, 1);
        Directory.CreateDirectory(Path.GetDirectoryName(_path) ?? ".");
    }

    public void Write(string message)
    {
        lock (_lock)
        {
            RotateIfNeeded();
            File.AppendAllText(_path, message + Environment.NewLine);
        }
    }

    private void RotateIfNeeded()
    {
        if (!File.Exists(_path))
        {
            return;
        }

        var length = new FileInfo(_path).Length;
        if (length < _maxBytes)
        {
            return;
        }

        for (var i = _maxFiles - 1; i >= 1; i--)
        {
            var source = $"{_path}.{i}";
            var target = $"{_path}.{i + 1}";
            if (File.Exists(source))
            {
                if (i == _maxFiles - 1)
                {
                    File.Delete(source);
                }
                else
                {
                    File.Move(source, target, true);
                }
            }
        }

        File.Move(_path, _path + ".1", true);
    }
}
