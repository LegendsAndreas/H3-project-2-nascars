using System.ComponentModel.DataAnnotations;

namespace Game.Api.Contracts;

public sealed class CreateSessionRequest
{
    public Guid? SessionId { get; init; }
    [Required, StringLength(100, MinimumLength = 1)]
    public string TrackId { get; init; } = "main";
    [Range(1, 10000)]
    public int TargetLaps { get; init; } = 10;
}

public sealed class SaveResultRequest
{
    [Required, StringLength(100, MinimumLength = 1)]
    public string DisplayName { get; init; } = "";
    [Required, Range(0, int.MaxValue)]
    public int? Score { get; init; }
    [Required, Range(0, int.MaxValue)]
    public int? Laps { get; init; }
    [Range(1, long.MaxValue)]
    public long? ElapsedMilliseconds { get; init; }
}
