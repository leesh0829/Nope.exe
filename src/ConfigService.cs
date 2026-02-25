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
        return new AppConfig
        {
            Rules = [],
        };
    }
}
