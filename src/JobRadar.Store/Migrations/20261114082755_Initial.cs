#pragma warning disable CA1861, IDE0161 // EF-generated: constant column arrays and block-scoped namespace are the generator style
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobRadar.Store.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agent_usage",
                columns: table => new
                {
                    run_id = table.Column<string>(type: "TEXT", nullable: false),
                    agent = table.Column<string>(type: "TEXT", nullable: false),
                    model = table.Column<string>(type: "TEXT", nullable: false),
                    batches = table.Column<int>(type: "INTEGER", nullable: false),
                    offers = table.Column<int>(type: "INTEGER", nullable: false),
                    input_tokens = table.Column<int>(type: "INTEGER", nullable: false),
                    output_tokens = table.Column<int>(type: "INTEGER", nullable: false),
                    reasoning_tokens = table.Column<int>(type: "INTEGER", nullable: false),
                    cached_input_tokens = table.Column<int>(type: "INTEGER", nullable: false),
                    ai_credits = table.Column<double>(type: "REAL", nullable: true),
                    usd = table.Column<double>(type: "REAL", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_usage", x => new { x.run_id, x.agent, x.model });
                });

            migrationBuilder.CreateTable(
                name: "feedback",
                columns: table => new
                {
                    offer_id = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<string>(type: "TEXT", nullable: false),
                    verdict = table.Column<string>(type: "TEXT", nullable: false),
                    note = table.Column<string>(type: "TEXT", nullable: true),
                    author = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_feedback", x => new { x.offer_id, x.created_at });
                });

            migrationBuilder.CreateTable(
                name: "offer_fields",
                columns: table => new
                {
                    offer_id = table.Column<string>(type: "TEXT", nullable: false),
                    description_hash = table.Column<string>(type: "TEXT", nullable: false),
                    role_family = table.Column<string>(type: "TEXT", nullable: false),
                    stack_main = table.Column<string>(type: "TEXT", nullable: false),
                    seniority = table.Column<string>(type: "TEXT", nullable: false),
                    work_mode = table.Column<string>(type: "TEXT", nullable: false),
                    office_days_per_week = table.Column<int>(type: "INTEGER", nullable: true),
                    office_country = table.Column<string>(type: "TEXT", nullable: true),
                    languages = table.Column<string>(type: "TEXT", nullable: false),
                    b2b_allowed = table.Column<string>(type: "TEXT", nullable: false),
                    employer_type = table.Column<string>(type: "TEXT", nullable: false),
                    confidence = table.Column<string>(type: "TEXT", nullable: false),
                    evidence = table.Column<string>(type: "TEXT", nullable: false),
                    model = table.Column<string>(type: "TEXT", nullable: false),
                    prompt_version = table.Column<string>(type: "TEXT", nullable: false),
                    extracted_at = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_offer_fields", x => new { x.offer_id, x.description_hash });
                });

            migrationBuilder.CreateTable(
                name: "offers",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    source = table.Column<string>(type: "TEXT", nullable: false),
                    source_id = table.Column<string>(type: "TEXT", nullable: false),
                    url = table.Column<string>(type: "TEXT", nullable: false),
                    canonical_key = table.Column<string>(type: "TEXT", nullable: false),
                    primary_offer_id = table.Column<string>(type: "TEXT", nullable: true),
                    title = table.Column<string>(type: "TEXT", nullable: false),
                    company = table.Column<string>(type: "TEXT", nullable: false),
                    country = table.Column<string>(type: "TEXT", nullable: true),
                    location = table.Column<string>(type: "TEXT", nullable: true),
                    work_mode = table.Column<string>(type: "TEXT", nullable: false),
                    office_days_per_week = table.Column<int>(type: "INTEGER", nullable: true),
                    contract = table.Column<string>(type: "TEXT", nullable: true),
                    salary_min = table.Column<int>(type: "INTEGER", nullable: true),
                    salary_max = table.Column<int>(type: "INTEGER", nullable: true),
                    currency = table.Column<string>(type: "TEXT", nullable: true),
                    salary_period = table.Column<string>(type: "TEXT", nullable: true),
                    description = table.Column<string>(type: "TEXT", nullable: false),
                    description_hash = table.Column<string>(type: "TEXT", nullable: false),
                    posted_at = table.Column<string>(type: "TEXT", nullable: true),
                    first_seen_at = table.Column<string>(type: "TEXT", nullable: false),
                    last_seen_at = table.Column<string>(type: "TEXT", nullable: false),
                    lifecycle = table.Column<string>(type: "TEXT", nullable: false),
                    missed_runs = table.Column<int>(type: "INTEGER", nullable: false),
                    liveness = table.Column<string>(type: "TEXT", nullable: false),
                    last_checked_at = table.Column<string>(type: "TEXT", nullable: true),
                    expired_at = table.Column<string>(type: "TEXT", nullable: true),
                    prefilter_reason = table.Column<string>(type: "TEXT", nullable: true),
                    needs_review = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_offers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "runs",
                columns: table => new
                {
                    run_id = table.Column<string>(type: "TEXT", nullable: false),
                    source = table.Column<string>(type: "TEXT", nullable: false),
                    kind = table.Column<string>(type: "TEXT", nullable: false),
                    started_at = table.Column<string>(type: "TEXT", nullable: false),
                    finished_at = table.Column<string>(type: "TEXT", nullable: true),
                    offers_found = table.Column<int>(type: "INTEGER", nullable: false),
                    offers_new = table.Column<int>(type: "INTEGER", nullable: false),
                    offers_changed = table.Column<int>(type: "INTEGER", nullable: false),
                    errors = table.Column<string>(type: "TEXT", nullable: false),
                    healthy = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_runs", x => new { x.run_id, x.source, x.kind });
                });

            migrationBuilder.CreateTable(
                name: "scores",
                columns: table => new
                {
                    offer_id = table.Column<string>(type: "TEXT", nullable: false),
                    description_hash = table.Column<string>(type: "TEXT", nullable: false),
                    criteria_version = table.Column<string>(type: "TEXT", nullable: false),
                    track = table.Column<string>(type: "TEXT", nullable: false),
                    score = table.Column<int>(type: "INTEGER", nullable: false),
                    reasons = table.Column<string>(type: "TEXT", nullable: false),
                    red_flags = table.Column<string>(type: "TEXT", nullable: false),
                    capped_by = table.Column<string>(type: "TEXT", nullable: false),
                    model = table.Column<string>(type: "TEXT", nullable: false),
                    prompt_version = table.Column<string>(type: "TEXT", nullable: false),
                    scored_at = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scores", x => new { x.offer_id, x.description_hash, x.criteria_version });
                });

            migrationBuilder.CreateIndex(
                name: "ix_offers_canonical_key",
                table: "offers",
                column: "canonical_key");

            migrationBuilder.CreateIndex(
                name: "ix_offers_lifecycle",
                table: "offers",
                column: "lifecycle");

            migrationBuilder.CreateIndex(
                name: "ux_offers_source_source_id",
                table: "offers",
                columns: new[] { "source", "source_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_usage");

            migrationBuilder.DropTable(
                name: "feedback");

            migrationBuilder.DropTable(
                name: "offer_fields");

            migrationBuilder.DropTable(
                name: "offers");

            migrationBuilder.DropTable(
                name: "runs");

            migrationBuilder.DropTable(
                name: "scores");
        }
    }
}
#pragma warning restore CA1861, IDE0161
