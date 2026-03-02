namespace ConnectWiseManager.Models;

public class ScriptExecution
{
    public string SessionId { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string ScriptType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string Output { get; set; } = string.Empty;
    public string? Error { get; set; }
}
