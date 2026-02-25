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
        foreach (var rule in config.Rules)
        {
            if (!rule.CompileRegex(out var error))
            {
                logger.Warn($"Rule '{rule.Name}' has invalid windowTitleRegex and will never match: {error}");
            }
        }

        return config;
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
        var config = new AppConfig
        {
            Rules =
            [
                new RuleConfig
                {
                    Name = "ExampleApp promotional popups",
                    Enabled = true,
                    Match = new MatchConfig
                    {
                        ProcessNameExact = "ExampleApp",
                        WindowTitleRegex = ".*Special Offer.*",
                    },
                    Action = new ActionConfig { Type = RuleActionType.Close },
                },
                new RuleConfig
                {
                    Name = "Example updater dialog",
                    Enabled = true,
                    Match = new MatchConfig
                    {
                        FileDescriptionContains = "Example Updater",
                        WindowClassExact = "#32770",
                    },
                    Action = new ActionConfig { Type = RuleActionType.Minimize },
                },
                new RuleConfig
                {
                    Name = "Example hidden helper",
                    Enabled = false,
                    Match = new MatchConfig
                    {
                        CompanyNameContains = "Example Corp",
                        WindowTitleRegex = ".*Helper.*",
                    },
                    Action = new ActionConfig { Type = RuleActionType.Hide },
                },
            ],
        };

        foreach (var rule in config.Rules)
        {
            rule.CompileRegex(out _);
        }

        return config;
    }
}
