using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pulse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHourlyRollups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "p95_ms",
                table: "monitor_daily_stats",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "monitor_hourly_stats",
                columns: table => new
                {
                    monitor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hour = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    checks = table.Column<int>(type: "integer", nullable: false),
                    successes = table.Column<int>(type: "integer", nullable: false),
                    avg_ms = table.Column<int>(type: "integer", nullable: true),
                    p95_ms = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_monitor_hourly_stats", x => new { x.monitor_id, x.hour });
                    table.ForeignKey(
                        name: "fk_monitor_hourly_stats_monitors_monitor_id",
                        column: x => x.monitor_id,
                        principalTable: "monitors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "monitor_hourly_stats");

            migrationBuilder.DropColumn(
                name: "p95_ms",
                table: "monitor_daily_stats");
        }
    }
}
