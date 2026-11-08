using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pulse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMonitorsAndChecks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "monitors",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    method = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    interval_seconds = table.Column<int>(type: "integer", nullable: false),
                    timeout_ms = table.Column<int>(type: "integer", nullable: false),
                    expected_codes = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    keyword = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    headers = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false),
                    failure_threshold = table.Column<int>(type: "integer", nullable: false),
                    recovery_threshold = table.Column<int>(type: "integer", nullable: false),
                    state = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    consecutive_failures = table.Column<int>(type: "integer", nullable: false),
                    consecutive_successes = table.Column<int>(type: "integer", nullable: false),
                    next_check_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    paused_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    badge_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    state_changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_checked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_response_ms = table.Column<int>(type: "integer", nullable: true),
                    last_status_code = table.Column<int>(type: "integer", nullable: true),
                    last_error_type = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    last_error_message = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_monitors", x => x.id);
                    table.ForeignKey(
                        name: "fk_monitors_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_monitors_next_check_at",
                table: "monitors",
                column: "next_check_at",
                filter: "state <> 'Paused'");

            migrationBuilder.CreateIndex(
                name: "ix_monitors_organization_id",
                table: "monitors",
                column: "organization_id");

            // check_results is partitioned by month on checked_at, so EF cannot create it. Old data is dropped by
            // detaching whole partitions, never with DELETE. The function below makes the partition for any month;
            // the worker calls it ahead of time (see PartitionMaintainer).
            migrationBuilder.Sql("""
                CREATE TABLE check_results (
                    monitor_id uuid NOT NULL,
                    checked_at timestamptz NOT NULL,
                    success boolean NOT NULL,
                    status_code integer NULL,
                    response_ms integer NOT NULL,
                    error_type varchar(24) NULL,
                    error_message varchar(300) NULL,
                    CONSTRAINT pk_check_results PRIMARY KEY (monitor_id, checked_at),
                    CONSTRAINT fk_check_results_monitors_monitor_id FOREIGN KEY (monitor_id) REFERENCES monitors (id) ON DELETE CASCADE
                ) PARTITION BY RANGE (checked_at);
                """);

            migrationBuilder.Sql("""
                CREATE FUNCTION pulse_ensure_check_partition(month_start date) RETURNS text
                LANGUAGE plpgsql AS $$
                DECLARE
                    first_day date := date_trunc('month', month_start)::date;
                    partition_name text := 'check_results_' || to_char(first_day, 'YYYY_MM');
                BEGIN
                    IF to_regclass(partition_name) IS NULL THEN
                        EXECUTE format(
                            'CREATE TABLE %I PARTITION OF check_results FOR VALUES FROM (%L) TO (%L)',
                            partition_name,
                            (first_day::timestamp AT TIME ZONE 'UTC'),
                            ((first_day + interval '1 month')::timestamp AT TIME ZONE 'UTC'));
                    END IF;
                    RETURN partition_name;
                END
                $$;
                """);

            // The current month and the next two exist from the start.
            migrationBuilder.Sql("""
                SELECT pulse_ensure_check_partition((date_trunc('month', now() AT TIME ZONE 'UTC') + make_interval(months => n))::date)
                FROM generate_series(0, 2) AS n;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE check_results;");
            migrationBuilder.Sql("DROP FUNCTION pulse_ensure_check_partition(date);");

            migrationBuilder.DropTable(
                name: "monitors");
        }
    }
}
