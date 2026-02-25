using System.Diagnostics;

namespace NopeExe;

public sealed class RuleEngine
{
    private readonly AppConfig _config;
    private readonly ILogger _logger;
    private readonly Dictionary<string, DateTimeOffset> _cooldowns = new();

    public RuleEngine(AppConfig config, ILogger logger)
    {
        _config = config;
        _logger = logger;
    }

    public void EvaluateAndAct(WindowSnapshot window)
    {
        foreach (var rule in _config.Rules.Where(r => r.Enabled))
        {
            if (!IsMatch(rule, window))
            {
                continue;
            }

            var key = $"{rule.Name}:{window.Handle}:{window.ProcessId}";
            if (_cooldowns.TryGetValue(key, out var nextAllowed) && nextAllowed > DateTimeOffset.Now)
            {
                continue;
            }

            var actionResult = ExecuteAction(rule, window);
            _cooldowns[key] = DateTimeOffset.Now.AddSeconds(_config.CooldownSeconds);
            _logger.Info($"Rule '{rule.Name}' matched hwnd=0x{window.Handle:X}, pid={window.ProcessId}, title='{window.WindowTitle}', action={rule.Action.Type}, success={actionResult}");
        }
    }

    private bool IsMatch(RuleConfig rule, WindowSnapshot window)
    {
        var m = rule.Match;

        if (!IsBlank(m.ProcessNameExact) && !EqualsIgnoreCase(window.ProcessName, m.ProcessNameExact))
            return false;
        if (!IsBlank(m.ProcessPathExact) && !EqualsIgnoreCase(window.ProcessPath, m.ProcessPathExact))
            return false;

        return true;
    }

    private bool ExecuteAction(RuleConfig rule, WindowSnapshot window)
    {
        return rule.Action.Type switch
        {
            RuleActionType.Close => CloseWindow(window.Handle, rule.Action.SendMessageTimeoutMs),
            RuleActionType.Minimize => Win32.ShowWindow(window.Handle, Win32.SwMinimize),
            RuleActionType.Hide => Win32.ShowWindow(window.Handle, Win32.SwHide),
            RuleActionType.KillProcess => KillProcess(window.ProcessId),
            _ => false,
        };
    }

    private static bool CloseWindow(nint hWnd, uint timeoutMs)
    {
        var result = Win32.SendMessageTimeout(hWnd, Win32.WmClose, nint.Zero, nint.Zero, Win32.SmtoAbortIfHung, timeoutMs, out _);
        return result != nint.Zero;
    }

    private bool KillProcess(uint pid)
    {
        try
        {
            using var process = Process.GetProcessById((int)pid);
            process.Kill(true);
            return true;
        }
        catch (Exception ex)
        {
            _logger.Warn($"Failed to kill process {pid}: {ex.Message}");
            return false;
        }
    }

    private static bool IsBlank(string? value) => string.IsNullOrWhiteSpace(value);
    private static bool EqualsIgnoreCase(string a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}

public sealed class MonitorService
{
    private readonly WindowEnumerator _enumerator;
    private readonly RuleEngine _engine;
    private readonly AppConfig _config;
    private readonly ILogger _logger;

    public bool IsPaused { get; private set; }

    public MonitorService(WindowEnumerator enumerator, RuleEngine engine, AppConfig config, ILogger logger)
    {
        _enumerator = enumerator;
        _engine = engine;
        _config = config;
        _logger = logger;
    }

    public void SetPaused(bool paused)
    {
        IsPaused = paused;
        _logger.Info(paused ? "Monitoring paused." : "Monitoring resumed.");
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        _logger.Info($"Monitor started. PollIntervalMs={_config.PollIntervalMs}, CooldownSeconds={_config.CooldownSeconds}");

        while (!cancellationToken.IsCancellationRequested)
        {
            if (!IsPaused)
            {
                var windows = _enumerator.Enumerate(_logger);
                foreach (var window in windows)
                {
                    _engine.EvaluateAndAct(window);
                }
            }

            try
            {
                await Task.Delay(_config.PollIntervalMs, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }

        _logger.Info("Monitor stopped.");
    }
}
