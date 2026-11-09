using Microsoft.EntityFrameworkCore;

namespace Game.Api.Data;

public sealed class GameDbContext(DbContextOptions<GameDbContext> options) : DbContext(options)
{
    public DbSet<GameSession> Sessions => Set<GameSession>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<SessionResult> Results => Set<SessionResult>();
    public DbSet<RecordEntry> Records => Set<RecordEntry>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<GameSession>(b =>
        {
            b.ToTable("game_sessions", t =>
            {
                t.HasCheckConstraint("ck_session_status", "status IN ('running', 'completed')");
                t.HasCheckConstraint("ck_session_target", "target_laps BETWEEN 1 AND 10000");
                t.HasCheckConstraint("ck_session_dates", "(status = 'running' AND finished_at IS NULL) OR (status = 'completed' AND finished_at IS NOT NULL AND finished_at >= started_at)");
            });
            b.HasKey(x => x.Id);
            b.Property(x => x.TrackId).HasMaxLength(100).IsRequired();
            b.Property(x => x.Status).HasMaxLength(20).IsRequired();
            b.HasIndex(x => x.StartedAt);
        });
        model.Entity<Player>(b =>
        {
            b.ToTable("players");
            b.HasKey(x => x.Id);
            b.Property(x => x.CarId).HasMaxLength(128).IsRequired();
            b.Property(x => x.DisplayName).HasMaxLength(100).IsRequired();
            b.HasIndex(x => x.CarId).IsUnique();
        });
        model.Entity<SessionResult>(b =>
        {
            b.ToTable("session_results", t =>
            {
                t.HasCheckConstraint("ck_result_score", "score >= 0");
                t.HasCheckConstraint("ck_result_laps", "laps >= 0");
                t.HasCheckConstraint("ck_result_elapsed", "elapsed_milliseconds IS NULL OR elapsed_milliseconds > 0");
            });
            b.HasKey(x => x.Id);
            b.Property(x => x.DisplayName).HasMaxLength(100).IsRequired();
            b.HasOne(x => x.Session).WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Player).WithMany().HasForeignKey(x => x.PlayerId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.SessionId, x.PlayerId }).IsUnique();
            b.HasIndex(x => new { x.PlayerId, x.Score });
        });
        model.Entity<RecordEntry>().HasNoKey().ToView("leaderboard_records");

        foreach (var entity in model.Model.GetEntityTypes())
            foreach (var property in entity.GetProperties())
                property.SetColumnName(string.Concat(property.Name.Select((c, i) =>
                    char.IsUpper(c) && i > 0 ? "_" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString())));
    }
}
