using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Crm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentProductivityAndCollaboration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgentTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AssignedAgentUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DueAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RemindAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReminderGeneration = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    ArchivedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentTasks_AspNetUsers_AssignedAgentUserId",
                        column: x => x.AssignedAgentUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AgentTasks_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AgentTasks_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuickReplyTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    ArchivedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuickReplyTemplates", x => x.Id);
                    table.CheckConstraint("CK_QuickReplyTemplates_Owner", "([Scope] = N'Personal' AND [OwnerUserId] IS NOT NULL) OR ([Scope] = N'Shared' AND [OwnerUserId] IS NULL)");
                    table.ForeignKey(
                        name: "FK_QuickReplyTemplates_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuickReplyTemplates_AspNetUsers_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuickReplyTemplates_AspNetUsers_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TicketCollaborators",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgentUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AddedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AddedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RemovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RemovedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketCollaborators", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketCollaborators_AspNetUsers_AddedByUserId",
                        column: x => x.AddedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TicketCollaborators_AspNetUsers_AgentUserId",
                        column: x => x.AgentUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TicketCollaborators_AspNetUsers_RemovedByUserId",
                        column: x => x.RemovedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TicketCollaborators_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TicketInternalMessages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketInternalMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketInternalMessages_AspNetUsers_AuthorUserId",
                        column: x => x.AuthorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TicketInternalMessages_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TaskReminderNotifications",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecipientUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReminderGeneration = table.Column<int>(type: "int", nullable: false),
                    ScheduledForUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IssuedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReadAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    TransferredFromUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TransferredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskReminderNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskReminderNotifications_AgentTasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "AgentTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaskReminderNotifications_AspNetUsers_RecipientUserId",
                        column: x => x.RecipientUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaskReminderNotifications_AspNetUsers_TransferredFromUserId",
                        column: x => x.TransferredFromUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentTasks_AssignedAgentUserId_IsArchived_Status_DueAtUtc_Id",
                table: "AgentTasks",
                columns: new[] { "AssignedAgentUserId", "IsArchived", "Status", "DueAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentTasks_AssignedAgentUserId_RemindAtUtc",
                table: "AgentTasks",
                columns: new[] { "AssignedAgentUserId", "RemindAtUtc" },
                filter: "[RemindAtUtc] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AgentTasks_CreatedByUserId",
                table: "AgentTasks",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentTasks_TicketId",
                table: "AgentTasks",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_QuickReplyTemplates_CreatedByUserId",
                table: "QuickReplyTemplates",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_QuickReplyTemplates_OwnerUserId_IsArchived_Title_Id",
                table: "QuickReplyTemplates",
                columns: new[] { "OwnerUserId", "IsArchived", "Title", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_QuickReplyTemplates_Scope_IsArchived_Title_Id",
                table: "QuickReplyTemplates",
                columns: new[] { "Scope", "IsArchived", "Title", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_QuickReplyTemplates_UpdatedByUserId",
                table: "QuickReplyTemplates",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskReminderNotifications_RecipientUserId_IssuedAtUtc_Id",
                table: "TaskReminderNotifications",
                columns: new[] { "RecipientUserId", "IssuedAtUtc", "Id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_TaskReminderNotifications_TransferredFromUserId",
                table: "TaskReminderNotifications",
                column: "TransferredFromUserId");

            migrationBuilder.CreateIndex(
                name: "UX_TaskReminderNotifications_TaskGeneration",
                table: "TaskReminderNotifications",
                columns: new[] { "TaskId", "ReminderGeneration" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TicketCollaborators_AddedByUserId",
                table: "TicketCollaborators",
                column: "AddedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketCollaborators_AgentUserId_RemovedAtUtc",
                table: "TicketCollaborators",
                columns: new[] { "AgentUserId", "RemovedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TicketCollaborators_RemovedByUserId",
                table: "TicketCollaborators",
                column: "RemovedByUserId");

            migrationBuilder.CreateIndex(
                name: "UX_TicketCollaborators_ActiveMembership",
                table: "TicketCollaborators",
                columns: new[] { "TicketId", "AgentUserId" },
                unique: true,
                filter: "[RemovedAtUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TicketInternalMessages_AuthorUserId",
                table: "TicketInternalMessages",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketInternalMessages_TicketId_CreatedAtUtc_Id",
                table: "TicketInternalMessages",
                columns: new[] { "TicketId", "CreatedAtUtc", "Id" },
                descending: new[] { false, true, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuickReplyTemplates");

            migrationBuilder.DropTable(
                name: "TaskReminderNotifications");

            migrationBuilder.DropTable(
                name: "TicketCollaborators");

            migrationBuilder.DropTable(
                name: "TicketInternalMessages");

            migrationBuilder.DropTable(
                name: "AgentTasks");
        }
    }
}
