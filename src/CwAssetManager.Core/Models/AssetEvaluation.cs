namespace CwAssetManager.Core.Models;

/// <summary>Compliance and health evaluation for a machine at a point in time.</summary>
public sealed class AssetEvaluation
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid MachineId { get; init; }
    public Machine? Machine { get; set; }
    public double ComplianceScore { get; set; }
    public double HealthScore { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset EvaluatedAt { get; init; } = DateTimeOffset.UtcNow;
}
