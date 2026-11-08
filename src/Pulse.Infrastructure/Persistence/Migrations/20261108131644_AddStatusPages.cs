using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pulse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStatusPages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "monitor_daily_stats",
                columns: table => new
                {
                    monitor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day = table.Column<DateOnly>(type: "date", nullable: false),
                    checks = table.Column<int>(type: "integer", nullable: false),
                    successes = table.Column<int>(type: "integer", nullable: false),
                    avg_ms = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_monitor_daily_stats", x => new { x.monitor_id, x.day });
                    table.ForeignKey(
                        name: "fk_monitor_daily_stats_monitors_monitor_id",
                        column: x => x.monitor_id,
                        principalTable: "monitors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "status_pages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    slug = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    title = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    accent_color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    footer = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    show_response_times = table.Column<bool>(type: "boolean", nullable: false),
                    logo_content_type = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    logo_data = table.Column<byte[]>(type: "bytea", nullable: true),
                    logo_version = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_status_pages", x => x.id);
                    table.ForeignKey(
                        name: "fk_status_pages_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "status_components",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    status_page_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    group_name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_status_components", x => x.id);
                    table.ForeignKey(
                        name: "fk_status_components_status_pages_status_page_id",
                        column: x => x.status_page_id,
                        principalTable: "status_pages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subscribers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    status_page_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    confirm_token = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: true),
                    unsubscribe_token = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_subscribers", x => x.id);
                    table.ForeignKey(
                        name: "fk_subscribers_status_pages_status_page_id",
                        column: x => x.status_page_id,
                        principalTable: "status_pages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "component_monitors",
                columns: table => new
                {
                    component_id = table.Column<Guid>(type: "uuid", nullable: false),
                    monitor_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_component_monitors", x => new { x.component_id, x.monitor_id });
                    table.ForeignKey(
                        name: "fk_component_monitors_monitors_monitor_id",
                        column: x => x.monitor_id,
                        principalTable: "monitors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_component_monitors_status_components_component_id",
                        column: x => x.component_id,
                        principalTable: "status_components",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "incident_components",
                columns: table => new
                {
                    incident_id = table.Column<Guid>(type: "uuid", nullable: false),
                    component_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_incident_components", x => new { x.incident_id, x.component_id });
                    table.ForeignKey(
                        name: "fk_incident_components_incidents_incident_id",
                        column: x => x.incident_id,
                        principalTable: "incidents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_incident_components_status_components_component_id",
                        column: x => x.component_id,
                        principalTable: "status_components",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_component_monitors_monitor_id",
                table: "component_monitors",
                column: "monitor_id");

            migrationBuilder.CreateIndex(
                name: "ix_incident_components_component_id",
                table: "incident_components",
                column: "component_id");

            migrationBuilder.CreateIndex(
                name: "ix_status_components_status_page_id_sort_order",
                table: "status_components",
                columns: new[] { "status_page_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_status_pages_organization_id",
                table: "status_pages",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_status_pages_slug",
                table: "status_pages",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_subscribers_confirm_token",
                table: "subscribers",
                column: "confirm_token",
                unique: true,
                filter: "confirm_token IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_subscribers_status_page_id_email",
                table: "subscribers",
                columns: new[] { "status_page_id", "email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_subscribers_unsubscribe_token",
                table: "subscribers",
                column: "unsubscribe_token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "component_monitors");

            migrationBuilder.DropTable(
                name: "incident_components");

            migrationBuilder.DropTable(
                name: "monitor_daily_stats");

            migrationBuilder.DropTable(
                name: "subscribers");

            migrationBuilder.DropTable(
                name: "status_components");

            migrationBuilder.DropTable(
                name: "status_pages");
        }
    }
}
