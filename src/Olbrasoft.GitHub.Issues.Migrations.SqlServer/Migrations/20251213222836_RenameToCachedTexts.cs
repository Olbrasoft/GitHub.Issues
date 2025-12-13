using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Olbrasoft.GitHub.Issues.Migrations.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class RenameToCachedTexts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TranslatedTexts_Issues_IssueId",
                table: "TranslatedTexts");

            migrationBuilder.DropForeignKey(
                name: "FK_TranslatedTexts_Languages_LanguageId",
                table: "TranslatedTexts");

            migrationBuilder.DropForeignKey(
                name: "FK_TranslatedTexts_TextTypes_TextTypeId",
                table: "TranslatedTexts");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TranslatedTexts",
                table: "TranslatedTexts");

            migrationBuilder.RenameTable(
                name: "TranslatedTexts",
                newName: "CachedTexts");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "CachedTexts",
                newName: "CachedAt");

            migrationBuilder.RenameIndex(
                name: "IX_TranslatedTexts_TextTypeId",
                table: "CachedTexts",
                newName: "IX_CachedTexts_TextTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_TranslatedTexts_IssueId",
                table: "CachedTexts",
                newName: "IX_CachedTexts_IssueId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CachedTexts",
                table: "CachedTexts",
                columns: new[] { "LanguageId", "TextTypeId", "IssueId" });

            migrationBuilder.AddForeignKey(
                name: "FK_CachedTexts_Issues_IssueId",
                table: "CachedTexts",
                column: "IssueId",
                principalTable: "Issues",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CachedTexts_Languages_LanguageId",
                table: "CachedTexts",
                column: "LanguageId",
                principalTable: "Languages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CachedTexts_TextTypes_TextTypeId",
                table: "CachedTexts",
                column: "TextTypeId",
                principalTable: "TextTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CachedTexts_Issues_IssueId",
                table: "CachedTexts");

            migrationBuilder.DropForeignKey(
                name: "FK_CachedTexts_Languages_LanguageId",
                table: "CachedTexts");

            migrationBuilder.DropForeignKey(
                name: "FK_CachedTexts_TextTypes_TextTypeId",
                table: "CachedTexts");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CachedTexts",
                table: "CachedTexts");

            migrationBuilder.RenameTable(
                name: "CachedTexts",
                newName: "TranslatedTexts");

            migrationBuilder.RenameColumn(
                name: "CachedAt",
                table: "TranslatedTexts",
                newName: "CreatedAt");

            migrationBuilder.RenameIndex(
                name: "IX_CachedTexts_TextTypeId",
                table: "TranslatedTexts",
                newName: "IX_TranslatedTexts_TextTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_CachedTexts_IssueId",
                table: "TranslatedTexts",
                newName: "IX_TranslatedTexts_IssueId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TranslatedTexts",
                table: "TranslatedTexts",
                columns: new[] { "LanguageId", "TextTypeId", "IssueId" });

            migrationBuilder.AddForeignKey(
                name: "FK_TranslatedTexts_Issues_IssueId",
                table: "TranslatedTexts",
                column: "IssueId",
                principalTable: "Issues",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TranslatedTexts_Languages_LanguageId",
                table: "TranslatedTexts",
                column: "LanguageId",
                principalTable: "Languages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TranslatedTexts_TextTypes_TextTypeId",
                table: "TranslatedTexts",
                column: "TextTypeId",
                principalTable: "TextTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
