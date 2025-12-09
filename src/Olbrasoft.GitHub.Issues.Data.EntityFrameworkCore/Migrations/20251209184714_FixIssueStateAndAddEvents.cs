using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class FixIssueStateAndAddEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "state",
                table: "issues");

            migrationBuilder.RenameColumn(
                name: "html_url",
                table: "issues",
                newName: "url");

            migrationBuilder.AddColumn<bool>(
                name: "is_open",
                table: "issues",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "event_types",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "issue_events",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    github_event_id = table.Column<long>(type: "bigint", nullable: false),
                    issue_id = table.Column<int>(type: "integer", nullable: false),
                    event_type_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actor_id = table.Column<int>(type: "integer", nullable: true),
                    actor_login = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_issue_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_issue_events_event_types_event_type_id",
                        column: x => x.event_type_id,
                        principalTable: "event_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_issue_events_issues_issue_id",
                        column: x => x.issue_id,
                        principalTable: "issues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "event_types",
                columns: new[] { "id", "name" },
                values: new object[,]
                {
                    { 1, "assigned" },
                    { 2, "automatic_base_change_failed" },
                    { 3, "automatic_base_change_succeeded" },
                    { 4, "base_ref_changed" },
                    { 5, "closed" },
                    { 6, "commented" },
                    { 7, "committed" },
                    { 8, "connected" },
                    { 9, "convert_to_draft" },
                    { 10, "converted_to_discussion" },
                    { 11, "cross-referenced" },
                    { 12, "demilestoned" },
                    { 13, "deployed" },
                    { 14, "deployment_environment_changed" },
                    { 15, "disconnected" },
                    { 16, "head_ref_deleted" },
                    { 17, "head_ref_restored" },
                    { 18, "head_ref_force_pushed" },
                    { 19, "labeled" },
                    { 20, "locked" },
                    { 21, "mentioned" },
                    { 22, "marked_as_duplicate" },
                    { 23, "merged" },
                    { 24, "milestoned" },
                    { 25, "pinned" },
                    { 26, "ready_for_review" },
                    { 27, "referenced" },
                    { 28, "renamed" },
                    { 29, "reopened" },
                    { 30, "review_dismissed" },
                    { 31, "review_requested" },
                    { 32, "review_request_removed" },
                    { 33, "reviewed" },
                    { 34, "subscribed" },
                    { 35, "transferred" },
                    { 36, "unassigned" },
                    { 37, "unlabeled" },
                    { 38, "unlocked" },
                    { 39, "unmarked_as_duplicate" },
                    { 40, "unpinned" },
                    { 41, "unsubscribed" },
                    { 42, "user_blocked" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_event_types_name",
                table: "event_types",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_issue_events_event_type_id",
                table: "issue_events",
                column: "event_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_issue_events_github_event_id",
                table: "issue_events",
                column: "github_event_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_issue_events_issue_id",
                table: "issue_events",
                column: "issue_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "issue_events");

            migrationBuilder.DropTable(
                name: "event_types");

            migrationBuilder.DropColumn(
                name: "is_open",
                table: "issues");

            migrationBuilder.RenameColumn(
                name: "url",
                table: "issues",
                newName: "html_url");

            migrationBuilder.AddColumn<string>(
                name: "state",
                table: "issues",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");
        }
    }
}
