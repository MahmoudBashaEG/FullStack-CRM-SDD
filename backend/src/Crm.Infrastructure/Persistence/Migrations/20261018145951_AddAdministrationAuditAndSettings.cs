using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Crm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAdministrationAuditAndSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdminAuditLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    TargetType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    TargetId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    TargetLabel = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Changes = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminAuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdminAuditLogs_AspNetUsers_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    SupportEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SupportPhone = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    SupportInformation = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationSettings", x => x.Id);
                    table.CheckConstraint("CK_OrganizationSettings_Singleton", "[Id] = 1");
                    table.ForeignKey(
                        name: "FK_OrganizationSettings_AspNetUsers_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // C9: the one settings row, not configured yet (all values NULL). Not HasData: keeps the model seed-free.
            migrationBuilder.Sql("INSERT INTO [OrganizationSettings] ([Id]) VALUES (1);");

            migrationBuilder.CreateIndex(
                name: "IX_AdminAuditLogs_ActorUserId",
                table: "AdminAuditLogs",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AdminAuditLogs_OccurredAtUtc_Id",
                table: "AdminAuditLogs",
                columns: new[] { "OccurredAtUtc", "Id" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationSettings_UpdatedByUserId",
                table: "OrganizationSettings",
                column: "UpdatedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminAuditLogs");

            migrationBuilder.DropTable(
                name: "OrganizationSettings");
        }
    }
}
