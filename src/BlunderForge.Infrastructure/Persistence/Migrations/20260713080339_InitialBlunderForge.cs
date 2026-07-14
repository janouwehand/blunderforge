using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlunderForge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialBlunderForge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppSettings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ValueJson = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSettings", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "Games",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    PlayerColorChoice = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    PlayerSide = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    OpponentElo = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Result = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    TerminationReason = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    InitialFen = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    CurrentFen = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Version = table.Column<uint>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Games", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GameReviews",
                columns: table => new
                {
                    GameId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Result = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    OverallQuality = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CriticalMovesJson = table.Column<string>(type: "TEXT", nullable: false),
                    WentWell = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    FutureFocus = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    UsedAi = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameReviews", x => x.GameId);
                    table.ForeignKey(
                        name: "FK_GameReviews_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Moves",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Ply = table.Column<int>(type: "INTEGER", nullable: false),
                    Color = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    San = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Uci = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    FenBefore = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    FenAfter = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    IsOpponentMove = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Moves", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Moves_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MoveAnalyses",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MoveId = table.Column<long>(type: "INTEGER", nullable: false),
                    EngineVersion = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    EngineSettingsJson = table.Column<string>(type: "TEXT", nullable: false),
                    EvaluationBefore = table.Column<int>(type: "INTEGER", nullable: true),
                    EvaluationAfter = table.Column<int>(type: "INTEGER", nullable: true),
                    CentipawnLoss = table.Column<int>(type: "INTEGER", nullable: true),
                    Classification = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    BestMoveUci = table.Column<string>(type: "TEXT", maxLength: 8, nullable: true),
                    CandidateMovesJson = table.Column<string>(type: "TEXT", nullable: false),
                    PrincipalVariationJson = table.Column<string>(type: "TEXT", nullable: false),
                    IsCritical = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MoveAnalyses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MoveAnalyses_Moves_MoveId",
                        column: x => x.MoveId,
                        principalTable: "Moves",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Games_Status",
                table: "Games",
                column: "Status",
                unique: true,
                filter: "Status = 'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_MoveAnalyses_MoveId",
                table: "MoveAnalyses",
                column: "MoveId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Moves_GameId_Ply",
                table: "Moves",
                columns: new[] { "GameId", "Ply" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppSettings");

            migrationBuilder.DropTable(
                name: "GameReviews");

            migrationBuilder.DropTable(
                name: "MoveAnalyses");

            migrationBuilder.DropTable(
                name: "Moves");

            migrationBuilder.DropTable(
                name: "Games");
        }
    }
}
