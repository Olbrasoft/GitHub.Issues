using System.Text.RegularExpressions;

namespace Olbrasoft.GitHub.Issues.Data.EntityFrameworkCore.Tests;

/// <summary>
/// Tests that validate EF Core migrations don't contain common issues.
/// These tests catch problems like invalid defaultValue for vector columns.
/// </summary>
public class MigrationValidationTests
{
    private readonly string _sqlServerMigrationsPath;
    private readonly string _postgreSqlMigrationsPath;

    public MigrationValidationTests()
    {
        // Find the solution root
        var currentDir = Directory.GetCurrentDirectory();
        var solutionRoot = FindSolutionRoot(currentDir);

        _sqlServerMigrationsPath = Path.Combine(solutionRoot,
            "src", "Olbrasoft.GitHub.Issues.Migrations.SqlServer", "Migrations");
        _postgreSqlMigrationsPath = Path.Combine(solutionRoot,
            "src", "Olbrasoft.GitHub.Issues.Migrations.PostgreSQL", "Migrations");
    }

    [Fact]
    public void SqlServerMigrations_ShouldNotHaveEmptyDefaultValueForVectorColumns()
    {
        // This test catches the bug where EF Core generates:
        //   defaultValue: ""
        // for vector(1024) columns, which is invalid in SQL Server

        if (!Directory.Exists(_sqlServerMigrationsPath))
        {
            return; // Skip if migrations directory doesn't exist
        }

        var migrationFiles = Directory.GetFiles(_sqlServerMigrationsPath, "*.cs")
            .Where(f => !f.EndsWith(".Designer.cs") && !f.Contains("ModelSnapshot"));

        var issues = new List<string>();

        foreach (var file in migrationFiles)
        {
            var content = File.ReadAllText(file);
            var fileName = Path.GetFileName(file);

            // Pattern: Look for AlterColumn with vector type and empty defaultValue
            // This is the problematic pattern that causes migration failures
            // Skip comments (lines starting with //)
            var lines = content.Split('\n')
                .Where(line => !line.TrimStart().StartsWith("//"));
            var codeContent = string.Join("\n", lines);

            if (codeContent.Contains("vector(") && codeContent.Contains("defaultValue: \"\""))
            {
                issues.Add($"{fileName}: Contains 'defaultValue: \"\"' with vector column - empty string is not valid for vector type");
            }
        }

        Assert.True(issues.Count == 0,
            $"Found invalid migration patterns:\n{string.Join("\n", issues)}");
    }

    [Fact]
    public void PostgreSqlMigrations_ShouldNotHaveEmptyDefaultValueForVectorColumns()
    {
        if (!Directory.Exists(_postgreSqlMigrationsPath))
        {
            return; // Skip if migrations directory doesn't exist
        }

        var migrationFiles = Directory.GetFiles(_postgreSqlMigrationsPath, "*.cs")
            .Where(f => !f.EndsWith(".Designer.cs") && !f.Contains("ModelSnapshot"));

        var issues = new List<string>();

        foreach (var file in migrationFiles)
        {
            var content = File.ReadAllText(file);
            var fileName = Path.GetFileName(file);

            // Skip comments
            var lines = content.Split('\n')
                .Where(line => !line.TrimStart().StartsWith("//"));
            var codeContent = string.Join("\n", lines);

            if (codeContent.Contains("vector(") && codeContent.Contains("defaultValue: \"\""))
            {
                issues.Add($"{fileName}: Contains 'defaultValue: \"\"' with vector column - empty string is not valid for vector type");
            }
        }

        Assert.True(issues.Count == 0,
            $"Found invalid migration patterns:\n{string.Join("\n", issues)}");
    }

    [Fact]
    public void AllMigrations_DownMethodsShouldNotHaveEmptyDefaultValue()
    {
        // Down() methods also often have this issue when reverting nullable to required
        var allPaths = new[] { _sqlServerMigrationsPath, _postgreSqlMigrationsPath };
        var issues = new List<string>();

        foreach (var path in allPaths)
        {
            if (!Directory.Exists(path)) continue;

            var migrationFiles = Directory.GetFiles(path, "*.cs")
                .Where(f => !f.EndsWith(".Designer.cs") && !f.Contains("ModelSnapshot"));

            foreach (var file in migrationFiles)
            {
                var content = File.ReadAllText(file);
                var fileName = Path.GetFileName(file);

                // Skip comments before checking
                var lines = content.Split('\n')
                    .Where(line => !line.TrimStart().StartsWith("//"));
                var codeContent = string.Join("\n", lines);

                // Find Down method and check for problematic pattern
                var downMethodMatch = Regex.Match(codeContent,
                    @"protected override void Down\(.*?\{(.*?)\}\s*\}",
                    RegexOptions.Singleline);

                if (downMethodMatch.Success)
                {
                    var downContent = downMethodMatch.Groups[1].Value;
                    if (downContent.Contains("vector(") && downContent.Contains("defaultValue: \"\""))
                    {
                        issues.Add($"{fileName} (Down method): Contains 'defaultValue: \"\"' with vector column");
                    }
                }
            }
        }

        Assert.True(issues.Count == 0,
            $"Found invalid migration patterns in Down() methods:\n{string.Join("\n", issues)}");
    }

    private static string FindSolutionRoot(string startPath)
    {
        var dir = new DirectoryInfo(startPath);
        while (dir != null)
        {
            if (dir.GetFiles("*.sln").Any())
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }

        // Fallback: go up from test directory
        return Path.GetFullPath(Path.Combine(startPath, "..", "..", "..", ".."));
    }
}
