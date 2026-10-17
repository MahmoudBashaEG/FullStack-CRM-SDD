using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Crm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketWorkflowAndActivity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EscalatedAtUtc",
                table: "Tickets",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EscalatedByUserId",
                table: "Tickets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EscalationReason",
                table: "Tickets",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsEscalated",
                table: "Tickets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<byte[]>(
                name: "Version",
                table: "Tickets",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TicketActivities",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Field = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    OldValue = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FromAgentUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ToAgentUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketActivities_AspNetUsers_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TicketActivities_AspNetUsers_FromAgentUserId",
                        column: x => x.FromAgentUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TicketActivities_AspNetUsers_ToAgentUserId",
                        column: x => x.ToAgentUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TicketActivities_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_EscalatedByUserId",
                table: "Tickets",
                column: "EscalatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketActivities_ActorUserId",
                table: "TicketActivities",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketActivities_FromAgentUserId",
                table: "TicketActivities",
                column: "FromAgentUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketActivities_TicketId_OccurredAtUtc_Id",
                table: "TicketActivities",
                columns: new[] { "TicketId", "OccurredAtUtc", "Id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_TicketActivities_ToAgentUserId",
                table: "TicketActivities",
                column: "ToAgentUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_AspNetUsers_EscalatedByUserId",
                table: "Tickets",
                column: "EscalatedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_AspNetUsers_EscalatedByUserId",
                table: "Tickets");

            migrationBuilder.DropTable(
                name: "TicketActivities");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_EscalatedByUserId",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "EscalatedAtUtc",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "EscalatedByUserId",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "EscalationReason",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "IsEscalated",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Tickets");
        }
    }
}
