using System.Text.Json;

namespace NopeExe;

public sealed class ConfigService
{
    private readonly string _path;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    public ConfigService(string path)
    {
        _path = path;
    }

    public string Path => _path;

    public AppConfig LoadOrCreateDefault(ILogger logger)
    {
        if (!File.Exists(_path))
        {
            var defaultConfig = DefaultConfigFactory.Create();
            Save(defaultConfig);
            logger.Info($"Created default rules file at '{_path}'.");
            return defaultConfig;
        }

        var json = File.ReadAllText(_path);
        var config = JsonSerializer.Deserialize<AppConfig>(json, _jsonOptions) ?? new AppConfig();
        var originalCount = config.Rules.Count;
        config.Rules = config.Rules
            .Where(r => !IsLegacyExampleRule(r))
            .ToList();
        if (config.Rules.Count != originalCount)
        {
            logger.Info("Removed legacy bundled example rules from configuration.");
            Save(config);
        }

        foreach (var rule in config.Rules)
        {
            if (!rule.CompileRegex(out var error))
            {
                logger.Warn($"Rule '{rule.Name}' has invalid windowTitleRegex and will never match: {error}");
            }
        }

        return config;
    }

    private static bool IsLegacyExampleRule(RuleConfig rule)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ExampleApp promotional popup",
            "ExampleApp promotional popups",
            "Example updater dialog",
            "Example helper tool hidden",
            "Example hidden helper",
        };

        if (names.Contains(rule.Name))
        {
            return true;
        }

        return string.Equals(rule.Match.ProcessNameExact, "ExampleApp", StringComparison.OrdinalIgnoreCase)
            || string.Equals(rule.Match.CompanyNameContains, "Example Corp", StringComparison.OrdinalIgnoreCase);
    }

    public void Save(AppConfig config)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_path) ?? ".");
        var json = JsonSerializer.Serialize(config, _jsonOptions);
        File.WriteAllText(_path, json);
    }
}

public static class DefaultConfigFactory
{
    public static AppConfig Create()
    {
        return new AppConfig
        {
            Rules = [],
        };
    }
}
