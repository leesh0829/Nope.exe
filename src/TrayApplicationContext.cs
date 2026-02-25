using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace NopeExe;

public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly MonitorService _monitor;
    private readonly CancellationTokenSource _cts;
    private readonly ILogger _logger;
    private readonly string _rulesPath;
    private readonly string _logPath;
    private readonly ToolStripMenuItem _pauseResumeItem;

    public TrayApplicationContext(MonitorService monitor, CancellationTokenSource cts, ILogger logger, string rulesPath, string logPath)
    {
        _monitor = monitor;
        _cts = cts;
        _logger = logger;
        _rulesPath = rulesPath;
        _logPath = logPath;

        _pauseResumeItem = new ToolStripMenuItem("일시정지", null, (_, _) => TogglePause());

        var menu = new ContextMenuStrip();
        menu.Items.Add(_pauseResumeItem);
        menu.Items.Add(new ToolStripMenuItem("로그 보기", null, (_, _) => OpenPath(_logPath)));
        menu.Items.Add(new ToolStripMenuItem("설정 열기", null, (_, _) => OpenPath(_rulesPath)));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("종료", null, (_, _) => Exit()));

        _notifyIcon = new NotifyIcon
        {
            Text = "Nope.exe - Window Auto Closer",
            Icon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location) ?? SystemIcons.Application,
            ContextMenuStrip = menu,
            Visible = true,
        };

        _notifyIcon.DoubleClick += (_, _) => TogglePause();

        _ = Task.Run(() => _monitor.RunAsync(_cts.Token));
        _logger.Info("Tray mode started.");
    }

    private void TogglePause()
    {
        var newPausedState = !_monitor.IsPaused;
        _monitor.SetPaused(newPausedState);
        _pauseResumeItem.Text = newPausedState ? "재개" : "일시정지";
    }

    private void OpenPath(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            _logger.Warn($"Failed to open '{path}': {ex.Message}");
        }
    }

    private void Exit()
    {
        _notifyIcon.Visible = false;
        _cts.Cancel();
        _logger.Info("Tray exit requested.");
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _notifyIcon.Dispose();
            _cts.Dispose();
        }

        base.Dispose(disposing);
    }
}
