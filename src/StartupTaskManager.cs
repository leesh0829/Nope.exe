using System.Diagnostics;

namespace NopeExe;

public static class StartupTaskManager
{
    private const string TaskName = "NopeExeWindowAutoCloser";

    public static int Install(string exePath)
    {
        var args = $"/Create /F /TN \"{TaskName}\" /SC ONLOGON /TR \"\\\"{exePath}\\\"\"";
        return RunSchtasks(args);
    }

    public static int Uninstall()
    {
        var args = $"/Delete /F /TN \"{TaskName}\"";
        return RunSchtasks(args);
    }

    private static int RunSchtasks(string args)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "schtasks",
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            Console.WriteLine("Failed to launch schtasks.exe.");
            return 1;
        }

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (!string.IsNullOrWhiteSpace(output))
        {
            Console.WriteLine(output.Trim());
        }

        if (!string.IsNullOrWhiteSpace(error))
        {
            Console.Error.WriteLine(error.Trim());
        }

        if (process.ExitCode != 0)
        {
            Console.WriteLine("Task Scheduler command failed. Try running terminal as administrator if required by your policy.");
        }

        return process.ExitCode;
    }
}
