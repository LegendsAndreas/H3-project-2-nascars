using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Game.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialGameDatabase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "game_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    track_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    target_laps = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_sessions", x => x.id);
                    table.CheckConstraint("ck_session_dates", "(status = 'running' AND finished_at IS NULL) OR (status = 'completed' AND finished_at IS NOT NULL AND finished_at >= started_at)");
                    table.CheckConstraint("ck_session_status", "status IN ('running', 'completed')");
                    table.CheckConstraint("ck_session_target", "target_laps BETWEEN 1 AND 10000");
                });

            migrationBuilder.CreateTable(
                name: "players",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    car_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    display_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_players", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "session_results",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    display_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    score = table.Column<int>(type: "integer", nullable: false),
                    laps = table.Column<int>(type: "integer", nullable: false),
                    elapsed_milliseconds = table.Column<long>(type: "bigint", nullable: true),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_results", x => x.id);
                    table.CheckConstraint("ck_result_elapsed", "elapsed_milliseconds IS NULL OR elapsed_milliseconds > 0");
                    table.CheckConstraint("ck_result_laps", "laps >= 0");
                    table.CheckConstraint("ck_result_score", "score >= 0");
                    table.ForeignKey(
                        name: "FK_session_results_game_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "game_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_session_results_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_game_sessions_started_at",
                table: "game_sessions",
                column: "started_at");

            migrationBuilder.CreateIndex(
                name: "IX_players_car_id",
                table: "players",
                column: "car_id",
                unique: true);
            migrationBuilder.Sql("""
                CREATE VIEW leaderboard_records AS
                SELECT player_id, session_id, track_id, car_id, display_name,
                       score, laps, elapsed_milliseconds, recorded_at
                FROM (
                    SELECT r.player_id, r.session_id, s.track_id, p.car_id, r.display_name,
                           r.score, r.laps, r.elapsed_milliseconds, r.recorded_at,
                           ROW_NUMBER() OVER (
                               PARTITION BY r.player_id, s.track_id
                               ORDER BY r.score DESC, r.laps DESC,
                                        r.elapsed_milliseconds ASC NULLS LAST,
                                        r.recorded_at ASC, r.id ASC
                           ) AS result_rank
                    FROM session_results r
                    JOIN game_sessions s ON s.id = r.session_id
                    JOIN players p ON p.id = r.player_id
                    WHERE s.status = 'completed'
                ) ranked
                WHERE result_rank = 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_session_results_player_id_score",
                table: "session_results",
                columns: new[] { "player_id", "score" });

            migrationBuilder.CreateIndex(
                name: "IX_session_results_session_id_player_id",
                table: "session_results",
                columns: new[] { "session_id", "player_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS leaderboard_records;");
            migrationBuilder.DropTable(
                name: "session_results");

            migrationBuilder.DropTable(
                name: "game_sessions");

            migrationBuilder.DropTable(
                name: "players");
        }
    }
}
