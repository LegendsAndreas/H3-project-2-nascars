namespace Game.Api.Data;

public sealed class GameSession
{
    public Guid Id { get; set; }
    public string TrackId { get; set; } = "main";
    public int TargetLaps { get; set; } = 10;
    public string Status { get; set; } = "running";
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
}

public sealed class Player
{
    public Guid Id { get; set; }
    public string CarId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class SessionResult
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public Guid PlayerId { get; set; }
    public string DisplayName { get; set; } = "";
    public int Score { get; set; }
    public int Laps { get; set; }
    public long? ElapsedMilliseconds { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
    public Player Player { get; set; } = null!;
    public GameSession Session { get; set; } = null!;
}

public sealed class RecordEntry
{
    public Guid PlayerId { get; set; }
    public Guid SessionId { get; set; }
    public string TrackId { get; set; } = "";
    public string CarId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int Score { get; set; }
    public int Laps { get; set; }
    public long? ElapsedMilliseconds { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
}
