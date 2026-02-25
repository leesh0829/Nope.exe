using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace NopeExe;

public static class Win32
{
    public const uint WmClose = 0x0010;
    public const uint SmtoAbortIfHung = 0x0002;
    public const int SwHide = 0;
    public const int SwMinimize = 6;

    public delegate bool EnumWindowsProc(nint hWnd, nint lParam);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern int GetWindowText(nint hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern int GetClassName(nint hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(nint hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern nint SendMessageTimeout(
        nint hWnd,
        uint msg,
        nint wParam,
        nint lParam,
        uint fuFlags,
        uint uTimeout,
        out nint lpdwResult);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(nint hWnd, int nCmdShow);
}

public sealed class WindowEnumerator
{
    public IReadOnlyList<WindowSnapshot> Enumerate(ILogger logger)
    {
        var windows = new List<WindowSnapshot>();

        Win32.EnumWindows((hWnd, _) =>
        {
            if (!Win32.IsWindowVisible(hWnd))
            {
                return true;
            }

            Win32.GetWindowThreadProcessId(hWnd, out var pid);
            if (pid == 0)
            {
                return true;
            }

            var title = GetWindowText(hWnd);
            var className = GetClassName(hWnd);

            try
            {
                using var process = Process.GetProcessById((int)pid);
                var processName = process.ProcessName;
                string processPath = string.Empty;
                string fileDescription = string.Empty;
                string companyName = string.Empty;

                try
                {
                    processPath = process.MainModule?.FileName ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(processPath) && File.Exists(processPath))
                    {
                        var info = FileVersionInfo.GetVersionInfo(processPath);
                        fileDescription = info.FileDescription ?? string.Empty;
                        companyName = info.CompanyName ?? string.Empty;
                    }
                }
                catch
                {
                    // Access denied process metadata is expected sometimes.
                }

                windows.Add(new WindowSnapshot
                {
                    Handle = hWnd,
                    ProcessId = pid,
                    WindowTitle = title,
                    WindowClass = className,
                    ProcessName = processName,
                    ProcessPath = processPath,
                    FileDescription = fileDescription,
                    CompanyName = companyName,
                });
            }
            catch (Exception ex)
            {
                logger.Warn($"Failed to inspect window 0x{hWnd:X}: {ex.Message}");
            }

            return true;
        }, nint.Zero);

        return windows;
    }

    private static string GetWindowText(nint handle)
    {
        var sb = new StringBuilder(1024);
        _ = Win32.GetWindowText(handle, sb, sb.Capacity);
        return sb.ToString();
    }

    private static string GetClassName(nint handle)
    {
        var sb = new StringBuilder(256);
        _ = Win32.GetClassName(handle, sb, sb.Capacity);
        return sb.ToString();
    }
}
