using System.Text.Json.Serialization;

namespace NopeExe;

public sealed class AppConfig
{
    public int PollIntervalMs { get; set; } = 750;
    public int CooldownSeconds { get; set; } = 10;
    public string LogFilePath { get; set; } = Path.Combine("logs", "app.log");
    public long LogMaxBytes { get; set; } = 5 * 1024 * 1024;
    public int LogMaxFiles { get; set; } = 5;
    public List<RuleConfig> Rules { get; set; } = new();
}

public sealed class RuleConfig
{
    public string Name { get; set; } = "Unnamed rule";
    public bool Enabled { get; set; } = true;
    public MatchConfig Match { get; set; } = new();
    public ActionConfig Action { get; set; } = new();

}

public sealed class MatchConfig
{
    public string? ProcessNameExact { get; set; }
    public string? ProcessPathExact { get; set; }
}

public sealed class ActionConfig
{
    public RuleActionType Type { get; set; } = RuleActionType.Close;
    public uint SendMessageTimeoutMs { get; set; } = 1500;
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RuleActionType
{
    Close,
    Minimize,
    Hide,
    KillProcess,
}

public sealed class WindowSnapshot
{
    public nint Handle { get; init; }
    public uint ProcessId { get; init; }
    public string WindowTitle { get; init; } = string.Empty;
    public string WindowClass { get; init; } = string.Empty;
    public string ProcessName { get; init; } = string.Empty;
    public string ProcessPath { get; init; } = string.Empty;
    public string FileDescription { get; init; } = string.Empty;
    public string CompanyName { get; init; } = string.Empty;
}
