using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Crm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialTicketStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dbo");

            migrationBuilder.CreateTable(
                name: "AgentReferences",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Issuer = table.Column<string>(type: "nvarchar(max)", nullable: false, collation: "Latin1_General_100_BIN2"),
                    Subject = table.Column<string>(type: "nvarchar(max)", nullable: false, collation: "Latin1_General_100_BIN2"),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsEligible = table.Column<bool>(type: "bit", nullable: false),
                    IdentityKey = table.Column<byte[]>(type: "varbinary(900)", nullable: true, computedColumnSql: "CONVERT(binary(32), HASHBYTES('SHA2_256', CONVERT(nvarchar(max), DATALENGTH([Issuer])) + N':' + [Issuer] + CONVERT(nvarchar(max), DATALENGTH([Subject])) + N':' + [Subject]))", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentReferences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CategoryReferences",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsAvailable = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoryReferences", x => x.Id);
                    table.CheckConstraint("CK_CategoryReferences_Fallback", "[Id] <> '00000000-0000-0000-0000-000000000001' OR [IsAvailable] = 1");
                });

            migrationBuilder.CreateTable(
                name: "Tickets",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false, collation: "Latin1_General_100_BIN2"),
                    Subject = table.Column<string>(type: "nvarchar(max)", nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Priority = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    AssignedAgentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tickets", x => x.Id);
                    table.CheckConstraint("CK_Tickets_Priority", "[Priority] IN ('Low','Normal','High','Urgent')");
                    table.CheckConstraint("CK_Tickets_Revision", "[Revision] >= 1");
                    table.CheckConstraint("CK_Tickets_Status", "[Status] IN ('Open','InProgress','Resolved','Closed')");
                    table.ForeignKey(
                        name: "FK_Tickets_AgentReferences_AssignedAgentId",
                        column: x => x.AssignedAgentId,
                        principalSchema: "dbo",
                        principalTable: "AgentReferences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tickets_CategoryReferences_CategoryId",
                        column: x => x.CategoryId,
                        principalSchema: "dbo",
                        principalTable: "CategoryReferences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TicketHistoryEntries",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Field = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    PreviousValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ActorIssuer = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ActorSubject = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ActorDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketHistoryEntries", x => x.Id);
                    table.CheckConstraint("CK_TicketHistory_Order", "[Revision] >= 1 AND [Sequence] >= 1");
                    table.ForeignKey(
                        name: "FK_TicketHistoryEntries_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalSchema: "dbo",
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "dbo",
                table: "CategoryReferences",
                columns: new[] { "Id", "DisplayName", "IsAvailable" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), "Uncategorised", true });

            migrationBuilder.CreateIndex(
                name: "IX_AgentReferences_IdentityKey",
                schema: "dbo",
                table: "AgentReferences",
                column: "IdentityKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TicketHistoryEntries_TicketId_Revision_Sequence",
                schema: "dbo",
                table: "TicketHistoryEntries",
                columns: new[] { "TicketId", "Revision", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_AssignedAgentId",
                schema: "dbo",
                table: "Tickets",
                column: "AssignedAgentId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_CategoryId",
                schema: "dbo",
                table: "Tickets",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_Reference",
                schema: "dbo",
                table: "Tickets",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_UpdatedAtUtc_Reference",
                schema: "dbo",
                table: "Tickets",
                columns: new[] { "UpdatedAtUtc", "Reference" },
                descending: new[] { true, false });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TicketHistoryEntries",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Tickets",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AgentReferences",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "CategoryReferences",
                schema: "dbo");
        }
    }
}
