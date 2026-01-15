using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SelfCodeSupport.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPersistentDataEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnalysisCache",
                columns: table => new
                {
                    CacheKey = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    TicketId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    TicketHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    AnalysisJson = table.Column<string>(type: "TEXT", nullable: false),
                    CachedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastAccessedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalysisCache", x => x.CacheKey);
                });

            migrationBuilder.CreateTable(
                name: "SavedAnalyses",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    TicketId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    TicketTitle = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    AnalysisJson = table.Column<string>(type: "TEXT", nullable: false),
                    SavedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    SentToJira = table.Column<bool>(type: "INTEGER", nullable: false),
                    UsedForImplementation = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedAnalyses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowProgress",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TicketId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Phase = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    State = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ProgressPercentage = table.Column<int>(type: "INTEGER", nullable: false),
                    Message = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowProgress", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Workflows",
                columns: table => new
                {
                    TicketId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    TicketTitle = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CurrentPhase = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    State = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    AnalysisJson = table.Column<string>(type: "TEXT", nullable: true),
                    ImplementationJson = table.Column<string>(type: "TEXT", nullable: true),
                    PullRequestJson = table.Column<string>(type: "TEXT", nullable: true),
                    ErrorsJson = table.Column<string>(type: "TEXT", nullable: true),
                    IsSuccess = table.Column<bool>(type: "INTEGER", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastUpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workflows", x => x.TicketId);
                });

            migrationBuilder.UpdateData(
                table: "ApplicationSettings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 1, 15, 14, 54, 57, 387, DateTimeKind.Utc).AddTicks(4817), new DateTime(2026, 1, 15, 14, 54, 57, 387, DateTimeKind.Utc).AddTicks(4818) });

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisCache_ExpiresAt",
                table: "AnalysisCache",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisCache_LastAccessedAt",
                table: "AnalysisCache",
                column: "LastAccessedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisCache_TicketHash",
                table: "AnalysisCache",
                column: "TicketHash");

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisCache_TicketId",
                table: "AnalysisCache",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedAnalyses_CreatedAt",
                table: "SavedAnalyses",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SavedAnalyses_SavedAt",
                table: "SavedAnalyses",
                column: "SavedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SavedAnalyses_TicketId",
                table: "SavedAnalyses",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowProgress_TicketId_Timestamp",
                table: "WorkflowProgress",
                columns: new[] { "TicketId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_Workflows_LastUpdatedAt",
                table: "Workflows",
                column: "LastUpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Workflows_StartedAt",
                table: "Workflows",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Workflows_State",
                table: "Workflows",
                column: "State");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnalysisCache");

            migrationBuilder.DropTable(
                name: "SavedAnalyses");

            migrationBuilder.DropTable(
                name: "WorkflowProgress");

            migrationBuilder.DropTable(
                name: "Workflows");

            migrationBuilder.UpdateData(
                table: "ApplicationSettings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 1, 14, 17, 36, 50, 274, DateTimeKind.Utc).AddTicks(1630), new DateTime(2026, 1, 14, 17, 36, 50, 274, DateTimeKind.Utc).AddTicks(1631) });
        }
    }
}
