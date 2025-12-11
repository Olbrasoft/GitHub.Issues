using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Pgvector;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Olbrasoft.GitHub.Issues.Migrations.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

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
                    table.PrimaryKey("pk_event_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "repositories",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    git_hub_id = table.Column<long>(type: "bigint", nullable: false),
                    full_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    html_url = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    last_synced_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_repositories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "issues",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    repository_id = table.Column<int>(type: "integer", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    is_open = table.Column<bool>(type: "boolean", nullable: false),
                    url = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    git_hub_updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    embedding = table.Column<Vector>(type: "vector(768)", nullable: false),
                    synced_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    parent_issue_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_issues", x => x.id);
                    table.ForeignKey(
                        name: "fk_issues_issues_parent_issue_id",
                        column: x => x.parent_issue_id,
                        principalTable: "issues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_issues_repositories_repository_id",
                        column: x => x.repository_id,
                        principalTable: "repositories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "labels",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    repository_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    color = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false, defaultValue: "ededed")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_labels", x => x.id);
                    table.ForeignKey(
                        name: "fk_labels_repositories_repository_id",
                        column: x => x.repository_id,
                        principalTable: "repositories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "issue_events",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    git_hub_event_id = table.Column<long>(type: "bigint", nullable: false),
                    issue_id = table.Column<int>(type: "integer", nullable: false),
                    event_type_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actor_id = table.Column<int>(type: "integer", nullable: true),
                    actor_login = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_issue_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_issue_events_event_types_event_type_id",
                        column: x => x.event_type_id,
                        principalTable: "event_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_issue_events_issues_issue_id",
                        column: x => x.issue_id,
                        principalTable: "issues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "issue_labels",
                columns: table => new
                {
                    issue_id = table.Column<int>(type: "integer", nullable: false),
                    label_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_issue_labels", x => new { x.issue_id, x.label_id });
                    table.ForeignKey(
                        name: "fk_issue_labels_issues_issue_id",
                        column: x => x.issue_id,
                        principalTable: "issues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_issue_labels_labels_label_id",
                        column: x => x.label_id,
                        principalTable: "labels",
                        principalColumn: "id");
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
                name: "ix_event_types_name",
                table: "event_types",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_issue_events_event_type_id",
                table: "issue_events",
                column: "event_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_issue_events_git_hub_event_id",
                table: "issue_events",
                column: "git_hub_event_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_issue_events_issue_id",
                table: "issue_events",
                column: "issue_id");

            migrationBuilder.CreateIndex(
                name: "ix_issue_labels_label_id",
                table: "issue_labels",
                column: "label_id");

            migrationBuilder.CreateIndex(
                name: "ix_issues_parent_issue_id",
                table: "issues",
                column: "parent_issue_id");

            migrationBuilder.CreateIndex(
                name: "ix_issues_repository_id_number",
                table: "issues",
                columns: new[] { "repository_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_labels_repository_id_name",
                table: "labels",
                columns: new[] { "repository_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_repositories_full_name",
                table: "repositories",
                column: "full_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_repositories_git_hub_id",
                table: "repositories",
                column: "git_hub_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "issue_events");

            migrationBuilder.DropTable(
                name: "issue_labels");

            migrationBuilder.DropTable(
                name: "event_types");

            migrationBuilder.DropTable(
                name: "issues");

            migrationBuilder.DropTable(
                name: "labels");

            migrationBuilder.DropTable(
                name: "repositories");
        }
    }
}
