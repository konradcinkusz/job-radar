using JobRadar.Store.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace JobRadar.Store;

/// <summary>The JobRadar SQLite schema (SPEC §6.4). Column names are the snake_case of the property names.</summary>
public sealed class JobRadarDbContext(DbContextOptions<JobRadarDbContext> options) : DbContext(options)
{
    public DbSet<OfferRow> Offers => Set<OfferRow>();
    public DbSet<OfferFieldsRow> OfferFields => Set<OfferFieldsRow>();
    public DbSet<ScoreRow> Scores => Set<ScoreRow>();
    public DbSet<RunRow> Runs => Set<RunRow>();
    public DbSet<AgentUsageRow> AgentUsage => Set<AgentUsageRow>();
    public DbSet<FeedbackRow> Feedback => Set<FeedbackRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OfferRow>(e =>
        {
            e.ToTable("offers");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.Source, x.SourceId }).IsUnique().HasDatabaseName("ux_offers_source_source_id");
            e.HasIndex(x => x.CanonicalKey).HasDatabaseName("ix_offers_canonical_key");
            e.HasIndex(x => x.Lifecycle).HasDatabaseName("ix_offers_lifecycle");
        });

        modelBuilder.Entity<OfferFieldsRow>(e =>
        {
            e.ToTable("offer_fields");
            e.HasKey(x => new { x.OfferId, x.DescriptionHash });
        });

        modelBuilder.Entity<ScoreRow>(e =>
        {
            e.ToTable("scores");
            e.HasKey(x => new { x.OfferId, x.DescriptionHash, x.CriteriaVersion });
        });

        modelBuilder.Entity<RunRow>(e =>
        {
            e.ToTable("runs");
            e.HasKey(x => new { x.RunId, x.Source, x.Kind });
        });

        modelBuilder.Entity<AgentUsageRow>(e =>
        {
            e.ToTable("agent_usage");
            e.HasKey(x => new { x.RunId, x.Agent, x.Model });
        });

        modelBuilder.Entity<FeedbackRow>(e =>
        {
            e.ToTable("feedback");
            e.HasKey(x => new { x.OfferId, x.CreatedAt });
        });

        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(ToSnakeCase(property.Name));
            }
        }
    }

    private static string ToSnakeCase(string name) =>
        string.Concat(name.Select((c, i) => char.IsUpper(c) && i > 0 ? "_" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));
}

/// <summary>Design-time factory so <c>dotnet ef</c> can build the model. It never opens its throw-away file.</summary>
public sealed class JobRadarDbContextFactory : IDesignTimeDbContextFactory<JobRadarDbContext>
{
    public JobRadarDbContext CreateDbContext(string[] args)
    {
        var path = Path.Combine(Path.GetTempPath(), "jobradar-design-" + Guid.NewGuid().ToString("N") + ".db");
        var options = new DbContextOptionsBuilder<JobRadarDbContext>().UseSqlite("Data Source=" + path).Options;
        return new JobRadarDbContext(options);
    }
}
