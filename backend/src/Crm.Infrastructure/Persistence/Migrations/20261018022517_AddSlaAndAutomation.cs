using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Crm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSlaAndAutomation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FirstRespondedAtUtc",
                table: "Tickets",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FirstRespondedByUserId",
                table: "Tickets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SlaFirstResponseDueAtUtc",
                table: "Tickets",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SlaFirstResponseTargetMinutes",
                table: "Tickets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SlaResolutionDueAtUtc",
                table: "Tickets",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SlaResolutionTargetMinutes",
                table: "Tickets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SlaResolvedAtUtc",
                table: "Tickets",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SlaTimeZoneId",
                table: "Tickets",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ActorUserId",
                table: "TicketActivities",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.CreateTable(
                name: "AutomationRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    EnabledAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Trigger = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CategoryCondition = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    PriorityCondition = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    AssignedCondition = table.Column<bool>(type: "bit", nullable: true),
                    SlaKindCondition = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    TargetAgentUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WarningLeadMinutes = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutomationRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AutomationRules_AspNetUsers_TargetAgentUserId",
                        column: x => x.TargetAgentUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AutomationRules_AspNetUsers_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BusinessCalendars",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    TimeZoneId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    WorkingDaysMask = table.Column<int>(type: "int", nullable: false),
                    OpensAt = table.Column<TimeOnly>(type: "time", nullable: false),
                    ClosesAt = table.Column<TimeOnly>(type: "time", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessCalendars", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessCalendars_AspNetUsers_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SlaPolicies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SlaPolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SlaPolicies_AspNetUsers_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AutomationRuleExecutions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SlaKind = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Trigger = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Detail = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutomationRuleExecutions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AutomationRuleExecutions_AutomationRules_RuleId",
                        column: x => x.RuleId,
                        principalTable: "AutomationRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AutomationRuleExecutions_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BusinessHolidays",
                columns: table => new
                {
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    BusinessCalendarId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessHolidays", x => new { x.BusinessCalendarId, x.Date });
                    table.ForeignKey(
                        name: "FK_BusinessHolidays_BusinessCalendars_BusinessCalendarId",
                        column: x => x.BusinessCalendarId,
                        principalTable: "BusinessCalendars",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SlaPolicyTargets",
                columns: table => new
                {
                    Priority = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SlaPolicyId = table.Column<int>(type: "int", nullable: false),
                    FirstResponseMinutes = table.Column<int>(type: "int", nullable: false),
                    ResolutionMinutes = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SlaPolicyTargets", x => new { x.SlaPolicyId, x.Priority });
                    table.ForeignKey(
                        name: "FK_SlaPolicyTargets_SlaPolicies_SlaPolicyId",
                        column: x => x.SlaPolicyId,
                        principalTable: "SlaPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SlaAlerts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExecutionId = table.Column<long>(type: "bigint", nullable: false),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecipientUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DueAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReadAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SlaAlerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SlaAlerts_AspNetUsers_RecipientUserId",
                        column: x => x.RecipientUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SlaAlerts_AutomationRuleExecutions_ExecutionId",
                        column: x => x.ExecutionId,
                        principalTable: "AutomationRuleExecutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SlaAlerts_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_FirstRespondedByUserId",
                table: "Tickets",
                column: "FirstRespondedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_SlaFirstResponseDueAtUtc",
                table: "Tickets",
                column: "SlaFirstResponseDueAtUtc",
                filter: "[SlaFirstResponseDueAtUtc] IS NOT NULL AND [FirstRespondedAtUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_SlaResolutionDueAtUtc",
                table: "Tickets",
                column: "SlaResolutionDueAtUtc",
                filter: "[SlaResolutionDueAtUtc] IS NOT NULL AND [SlaResolvedAtUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AutomationRuleExecutions_OccurredAtUtc_Id",
                table: "AutomationRuleExecutions",
                columns: new[] { "OccurredAtUtc", "Id" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_AutomationRuleExecutions_TicketId",
                table: "AutomationRuleExecutions",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "UX_AutomationRuleExecutions_RuleTicketKind",
                table: "AutomationRuleExecutions",
                columns: new[] { "RuleId", "TicketId", "SlaKind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AutomationRules_TargetAgentUserId",
                table: "AutomationRules",
                column: "TargetAgentUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AutomationRules_Trigger_IsEnabled_SortOrder_CreatedAtUtc_Id",
                table: "AutomationRules",
                columns: new[] { "Trigger", "IsEnabled", "SortOrder", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_AutomationRules_UpdatedByUserId",
                table: "AutomationRules",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessCalendars_UpdatedByUserId",
                table: "BusinessCalendars",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SlaAlerts_RecipientUserId_CreatedAtUtc_Id",
                table: "SlaAlerts",
                columns: new[] { "RecipientUserId", "CreatedAtUtc", "Id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_SlaAlerts_TicketId",
                table: "SlaAlerts",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "UX_SlaAlerts_ExecutionRecipient",
                table: "SlaAlerts",
                columns: new[] { "ExecutionId", "RecipientUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SlaPolicies_UpdatedByUserId",
                table: "SlaPolicies",
                column: "UpdatedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_AspNetUsers_FirstRespondedByUserId",
                table: "Tickets",
                column: "FirstRespondedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_AspNetUsers_FirstRespondedByUserId",
                table: "Tickets");

            migrationBuilder.DropTable(
                name: "BusinessHolidays");

            migrationBuilder.DropTable(
                name: "SlaAlerts");

            migrationBuilder.DropTable(
                name: "SlaPolicyTargets");

            migrationBuilder.DropTable(
                name: "BusinessCalendars");

            migrationBuilder.DropTable(
                name: "AutomationRuleExecutions");

            migrationBuilder.DropTable(
                name: "SlaPolicies");

            migrationBuilder.DropTable(
                name: "AutomationRules");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_FirstRespondedByUserId",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_SlaFirstResponseDueAtUtc",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_SlaResolutionDueAtUtc",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "FirstRespondedAtUtc",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "FirstRespondedByUserId",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "SlaFirstResponseDueAtUtc",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "SlaFirstResponseTargetMinutes",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "SlaResolutionDueAtUtc",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "SlaResolutionTargetMinutes",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "SlaResolvedAtUtc",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "SlaTimeZoneId",
                table: "Tickets");

            migrationBuilder.AlterColumn<Guid>(
                name: "ActorUserId",
                table: "TicketActivities",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}
