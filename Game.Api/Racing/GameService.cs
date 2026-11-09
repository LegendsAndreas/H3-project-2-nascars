using System.ComponentModel.DataAnnotations;
using Game.Api.Contracts;
using Game.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Game.Api.Racing;

public sealed class GameService(GameDbContext db, IConfiguration configuration)
{
    public static void Validate(object request)
    {
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, new ValidationContext(request), errors, true))
            throw new GameRequestException(400, string.Join(" ", errors.Select(x => x.ErrorMessage)));
    }

    private void RequireDatabase()
    {
        if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("GameDatabase")))
            throw new GameRequestException(503, "Configure ConnectionStrings:GameDatabase in User Secrets.");
    }

    public async Task<GameSession> CreateAsync(CreateSessionRequest request, CancellationToken token)
    {
        Validate(request);
        if (string.IsNullOrWhiteSpace(request.TrackId) || request.SessionId == Guid.Empty)
            throw new GameRequestException(400, "TrackId must be nonempty and SessionId must be a nonzero UUID.");
        RequireDatabase();
        var id = request.SessionId ?? Guid.NewGuid();
        var startedAt = DateTimeOffset.UtcNow;
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO game_sessions (id, track_id, target_laps, status, started_at)
            VALUES ({id}, {request.TrackId.Trim()}, {request.TargetLaps}, 'running', {startedAt})
            ON CONFLICT (id) DO NOTHING
            """, token);
        var session = await db.Sessions.AsNoTracking().SingleAsync(x => x.Id == id, token);
        if (session.TrackId != request.TrackId.Trim() || session.TargetLaps != request.TargetLaps)
            throw new GameRequestException(409, "Session ID already exists with different settings.");
        return session;
    }

    public async Task<IReadOnlyList<GameSession>> ListAsync(int limit, CancellationToken token)
    {
        if (limit is < 1 or > 100) throw new GameRequestException(400, "Limit must be between 1 and 100.");
        RequireDatabase();
        return await db.Sessions.AsNoTracking().OrderByDescending(x => x.StartedAt).ThenBy(x => x.Id).Take(limit).ToListAsync(token);
    }

    public async Task<GameSession> GetAsync(Guid id, CancellationToken token)
    {
        RequireDatabase();
        return await db.Sessions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, token)
            ?? throw new GameRequestException(404, "Session not found.");
    }

    public async Task<ResultResponse> SaveResultAsync(Guid id, string carId, SaveResultRequest request, CancellationToken token)
    {
        Validate(request);
        if (string.IsNullOrWhiteSpace(carId) || carId.Length > 128 || carId.Any(char.IsControl)
            || string.IsNullOrWhiteSpace(request.DisplayName))
            throw new GameRequestException(400, "Valid carId and displayName are required.");
        RequireDatabase();
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        var session = await db.Sessions.FromSqlInterpolated($"SELECT * FROM game_sessions WHERE id = {id} FOR UPDATE")
            .SingleOrDefaultAsync(token) ?? throw new GameRequestException(404, "Session not found.");
        var previous = await db.Results.Include(x => x.Player)
            .SingleOrDefaultAsync(x => x.SessionId == id && x.Player.CarId == carId, token);
        if (previous is not null)
        {
            if (previous.DisplayName != request.DisplayName.Trim() || previous.Score != request.Score
                || previous.Laps != request.Laps || previous.ElapsedMilliseconds != request.ElapsedMilliseconds)
                throw new GameRequestException(409, "A final result already exists for this car and session.");
            return ToResponse(previous, carId);
        }
        if (session.Status != "running") throw new GameRequestException(409, "Cannot add a result to a completed session.");
        if (request.Laps > session.TargetLaps) throw new GameRequestException(400, "Laps cannot exceed the session target.");
        var playerId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var displayName = request.DisplayName.Trim();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO players (id, car_id, display_name, created_at)
            VALUES ({playerId}, {carId}, {displayName}, {now})
            ON CONFLICT (car_id) DO UPDATE SET display_name = EXCLUDED.display_name
            """, token);
        playerId = await db.Players.Where(x => x.CarId == carId).Select(x => x.Id).SingleAsync(token);
        var result = new SessionResult
        {
            Id = Guid.NewGuid(), SessionId = id, PlayerId = playerId, DisplayName = displayName,
            Score = request.Score!.Value, Laps = request.Laps!.Value,
            ElapsedMilliseconds = request.ElapsedMilliseconds, RecordedAt = now
        };
        db.Results.Add(result);
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return ToResponse(result, carId);
    }

    public async Task<IReadOnlyList<ResultResponse>> ResultsAsync(Guid id, CancellationToken token)
    {
        await GetAsync(id, token);
        return await db.Results.AsNoTracking().Where(x => x.SessionId == id)
            .OrderByDescending(x => x.Score).ThenByDescending(x => x.Laps)
            .ThenBy(x => x.ElapsedMilliseconds == null).ThenBy(x => x.ElapsedMilliseconds).ThenBy(x => x.Id)
            .Select(x => new ResultResponse(x.Id, x.SessionId, x.Player.CarId, x.DisplayName, x.Score, x.Laps, x.ElapsedMilliseconds, x.RecordedAt))
            .ToListAsync(token);
    }

    public async Task<GameSession> CompleteAsync(Guid id, CancellationToken token)
    {
        RequireDatabase();
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        var session = await db.Sessions.FromSqlInterpolated($"SELECT * FROM game_sessions WHERE id = {id} FOR UPDATE")
            .SingleOrDefaultAsync(token) ?? throw new GameRequestException(404, "Session not found.");
        if (session.Status == "running")
        {
            session.Status = "completed";
            session.FinishedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(token);
        }
        await transaction.CommitAsync(token);
        return session;
    }

    public async Task<IReadOnlyList<RecordEntry>> LeaderboardAsync(string trackId, int limit, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(trackId) || trackId.Length > 100 || limit is < 1 or > 100)
            throw new GameRequestException(400, "A valid trackId and limit between 1 and 100 are required.");
        RequireDatabase();
        return await db.Records.AsNoTracking().Where(x => x.TrackId == trackId.Trim())
            .OrderByDescending(x => x.Score).ThenByDescending(x => x.Laps)
            .ThenBy(x => x.ElapsedMilliseconds == null).ThenBy(x => x.ElapsedMilliseconds)
            .ThenBy(x => x.RecordedAt).ThenBy(x => x.PlayerId).Take(limit).ToListAsync(token);
    }

    public async Task<ResultResponse> RecordUnityReportAsync(MqttResultRequest request, string trackId, int targetLaps, CancellationToken token)
    {
        Validate(request);
        if (string.IsNullOrWhiteSpace(request.CarName) || request.Laps > targetLaps)
            throw new GameRequestException(400, "carName must be nonempty and laps must not exceed the configured target.");
        RequireDatabase();
        var now = DateTimeOffset.UtcNow;
        // Unity supplies no stable event/controller ID or start time.
        // Preserve an independent attempt, without guessing identity or elapsed time.
        var session = new GameSession
        {
            Id = Guid.NewGuid(), TrackId = trackId, TargetLaps = targetLaps,
            Status = "completed", StartedAt = now, FinishedAt = now
        };
        var player = new Player
        {
            Id = Guid.NewGuid(), CarId = $"report-{Guid.NewGuid():N}",
            DisplayName = request.CarName.Trim(), CreatedAt = now
        };
        var result = new SessionResult
        {
            Id = Guid.NewGuid(), Session = session, SessionId = session.Id,
            Player = player, PlayerId = player.Id, DisplayName = player.DisplayName,
            Score = request.Laps!.Value, Laps = request.Laps.Value, RecordedAt = now
        };
        db.Results.Add(result);
        // EF commits all three rows together; no partly saved report.
        await db.SaveChangesAsync(token);
        return ToResponse(result, player.CarId);
    }

    private static ResultResponse ToResponse(SessionResult x, string carId) =>
        new(x.Id, x.SessionId, carId, x.DisplayName, x.Score, x.Laps, x.ElapsedMilliseconds, x.RecordedAt);
}
