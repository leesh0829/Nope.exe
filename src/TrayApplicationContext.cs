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
    private readonly ConfigService _configService;
    private readonly AppConfig _config;
    private readonly ToolStripMenuItem _pauseResumeItem;
    private SettingsForm? _settingsForm;

    public TrayApplicationContext(
        MonitorService monitor,
        CancellationTokenSource cts,
        ILogger logger,
        ConfigService configService,
        AppConfig config)
    {
        _monitor = monitor;
        _cts = cts;
        _logger = logger;
        _configService = configService;
        _config = config;

        _pauseResumeItem = new ToolStripMenuItem("일시정지", null, (_, _) => TogglePause());

        var menu = new ContextMenuStrip();
        menu.Items.Add(_pauseResumeItem);
        menu.Items.Add(new ToolStripMenuItem("로그 보기", null, (_, _) => OpenLogsFolder()));
        menu.Items.Add(new ToolStripMenuItem("설정 열기", null, (_, _) => OpenSettingsUi()));
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

    private void OpenLogsFolder()
    {
        var logDir = Path.Combine(AppContext.BaseDirectory, Path.GetDirectoryName(_config.LogFilePath) ?? "logs");
        try
        {
            Directory.CreateDirectory(logDir);
            OpenPath(logDir);
        }
        catch (Exception ex)
        {
            _logger.Warn($"Failed to open logs folder '{logDir}': {ex.Message}");
        }
    }

    private void OpenSettingsUi()
    {
        if (_settingsForm is not null && !_settingsForm.IsDisposed)
        {
            _settingsForm.BringToFront();
            _settingsForm.Focus();
            return;
        }

        _settingsForm = new SettingsForm(_config, _configService, _logger);
        _settingsForm.FormClosed += (_, _) => _settingsForm = null;
        _settingsForm.Show();
    }

    private void OpenPath(string path)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
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
            _settingsForm?.Dispose();
            _notifyIcon.Dispose();
            _cts.Dispose();
        }

        base.Dispose(disposing);
    }
}
