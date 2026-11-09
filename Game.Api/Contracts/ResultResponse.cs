namespace Game.Api.Contracts;

public sealed record ResultResponse(Guid Id, Guid SessionId, string CarId, string DisplayName,
    int Score, int Laps, long? ElapsedMilliseconds, DateTimeOffset RecordedAt);
