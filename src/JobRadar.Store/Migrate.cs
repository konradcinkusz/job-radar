using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.Store;

/// <summary>Applies the EF Core migrations to one SQLite file. Called by <c>jobradar migrate</c>, which every command runs first (SPEC §6.4).</summary>
public static class Migrate
{
    /// <summary>Applies pending migrations. The caller creates the folder. Running it again on the same file changes nothing.</summary>
    public static async Task RunAsync(string dbPath, CancellationToken ct = default)
    {
        var connectionString = new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString();
        var options = new DbContextOptionsBuilder<JobRadarDbContext>().UseSqlite(connectionString).Options;

        await using var db = new JobRadarDbContext(options);
        await db.Database.MigrateAsync(ct);
    }
}
