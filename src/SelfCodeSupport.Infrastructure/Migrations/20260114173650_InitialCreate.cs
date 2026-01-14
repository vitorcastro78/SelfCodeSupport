using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SelfCodeSupport.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApplicationSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    ApplicationName = table.Column<string>(type: "TEXT", nullable: false),
                    Version = table.Column<string>(type: "TEXT", nullable: false),
                    JiraSettingsJson = table.Column<string>(type: "TEXT", nullable: false),
                    GitSettingsJson = table.Column<string>(type: "TEXT", nullable: false),
                    AnthropicSettingsJson = table.Column<string>(type: "TEXT", nullable: false),
                    WorkflowSettingsJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProjectSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    JiraProjectKey = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    GitRepositoryPath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    GitRemoteUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    GitDefaultBranch = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    JiraSettingsJson = table.Column<string>(type: "TEXT", nullable: false),
                    GitSettingsJson = table.Column<string>(type: "TEXT", nullable: false),
                    WorkflowSettingsJson = table.Column<string>(type: "TEXT", nullable: false),
                    ProjectSpecificSettingsJson = table.Column<string>(type: "TEXT", nullable: false),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectSettings", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "ApplicationSettings",
                columns: new[] { "Id", "AnthropicSettingsJson", "ApplicationName", "CreatedAt", "GitSettingsJson", "JiraSettingsJson", "UpdatedAt", "UpdatedBy", "Version", "WorkflowSettingsJson" },
                values: new object[] { 1, "", "SelfCodeSupport", new DateTime(2026, 1, 14, 17, 36, 50, 274, DateTimeKind.Utc).AddTicks(1630), "", "", new DateTime(2026, 1, 14, 17, 36, 50, 274, DateTimeKind.Utc).AddTicks(1631), "", "1.0.0", "" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSettings_IsActive",
                table: "ProjectSettings",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSettings_IsDefault",
                table: "ProjectSettings",
                column: "IsDefault");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSettings_JiraProjectKey",
                table: "ProjectSettings",
                column: "JiraProjectKey");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSettings_Name",
                table: "ProjectSettings",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationSettings");

            migrationBuilder.DropTable(
                name: "ProjectSettings");
        }
    }
}
