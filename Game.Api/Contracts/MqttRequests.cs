using System.ComponentModel.DataAnnotations;

namespace Game.Api.Contracts;

// Matches Unity's current PublishCarToDb payload.
public sealed class MqttResultRequest
{
    [Required, StringLength(100, MinimumLength = 1)]
    public string CarName { get; init; } = "";
    [Required, Range(0, 10000)]
    public int? Laps { get; init; }
}

public sealed class MqttLeaderboardRequest
{
    [StringLength(100)]
    public string? RequestId { get; init; }
    [Required, StringLength(100, MinimumLength = 1)]
    public string TrackId { get; init; } = "main";
    [Range(1, 100)]
    public int Limit { get; init; } = 10;
}
