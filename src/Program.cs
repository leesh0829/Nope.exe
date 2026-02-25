using System.Windows.Forms;

namespace NopeExe;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var argSet = new HashSet<string>(args, StringComparer.OrdinalIgnoreCase);

        if (argSet.Contains("--install-startup"))
        {
            return StartupTaskManager.Install(Environment.ProcessPath ?? "Nope.exe");
        }

        if (argSet.Contains("--uninstall-startup"))
        {
            return StartupTaskManager.Uninstall();
        }

        var rulesPath = Path.Combine(AppContext.BaseDirectory, "rules.json");
        var bootstrapLogger = new CompositeLogger(new ConsoleLogSink());
        var configService = new ConfigService(rulesPath);
        var config = configService.LoadOrCreateDefault(bootstrapLogger);

        var sinks = new List<ILogSink>
        {
            new RollingFileLogSink(Path.Combine(AppContext.BaseDirectory, config.LogFilePath), config.LogMaxBytes, config.LogMaxFiles),
        };

        if (argSet.Contains("--console"))
        {
            sinks.Add(new ConsoleLogSink());
        }

        var logger = new CompositeLogger(sinks.ToArray());
        var enumerator = new WindowEnumerator();
        var engine = new RuleEngine(config, logger);
        var monitor = new MonitorService(enumerator, engine, config, logger);
        var cts = new CancellationTokenSource();

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };
        AppDomain.CurrentDomain.ProcessExit += (_, _) => cts.Cancel();

        if (argSet.Contains("--headless"))
        {
            logger.Info("Headless mode started.");
            monitor.RunAsync(cts.Token).GetAwaiter().GetResult();
            return 0;
        }

        if (argSet.Contains("--console"))
        {
            logger.Info("Console mode started.");
            monitor.RunAsync(cts.Token).GetAwaiter().GetResult();
            return 0;
        }

        ApplicationConfiguration.Initialize();
        using var context = new TrayApplicationContext(
            monitor,
            cts,
            logger,
            configService,
            config);
        Application.Run(context);
        return 0;
    }
}
