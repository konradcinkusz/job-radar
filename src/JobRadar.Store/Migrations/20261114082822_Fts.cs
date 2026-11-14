using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobRadar.Store.Migrations;

/// <summary>FTS5 external-content table over <c>offers(title, company, description)</c> and its three sync triggers.</summary>
public partial class Fts : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("CREATE VIRTUAL TABLE offers_fts USING fts5(title, company, description, content='offers', content_rowid='rowid');");
        migrationBuilder.Sql("CREATE TRIGGER offers_ai AFTER INSERT ON offers BEGIN INSERT INTO offers_fts(rowid, title, company, description) VALUES (new.rowid, new.title, new.company, new.description); END;");
        migrationBuilder.Sql("CREATE TRIGGER offers_ad AFTER DELETE ON offers BEGIN INSERT INTO offers_fts(offers_fts, rowid, title, company, description) VALUES ('delete', old.rowid, old.title, old.company, old.description); END;");
        migrationBuilder.Sql("CREATE TRIGGER offers_au AFTER UPDATE ON offers BEGIN INSERT INTO offers_fts(offers_fts, rowid, title, company, description) VALUES ('delete', old.rowid, old.title, old.company, old.description); INSERT INTO offers_fts(rowid, title, company, description) VALUES (new.rowid, new.title, new.company, new.description); END;");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS offers_au;");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS offers_ad;");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS offers_ai;");
        migrationBuilder.Sql("DROP TABLE IF EXISTS offers_fts;");
    }
}
