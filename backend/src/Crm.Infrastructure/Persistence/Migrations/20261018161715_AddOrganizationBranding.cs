using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Crm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationBranding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccentColor",
                table: "OrganizationSettings",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogoContentType",
                table: "OrganizationSettings",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LogoSizeBytes",
                table: "OrganizationSettings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LogoUpdatedAtUtc",
                table: "OrganizationSettings",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LogoVersion",
                table: "OrganizationSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "OrganizationLogos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationLogos", x => x.Id);
                    table.CheckConstraint("CK_OrganizationLogos_Singleton", "[Id] = 1");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrganizationLogos");

            migrationBuilder.DropColumn(
                name: "AccentColor",
                table: "OrganizationSettings");

            migrationBuilder.DropColumn(
                name: "LogoContentType",
                table: "OrganizationSettings");

            migrationBuilder.DropColumn(
                name: "LogoSizeBytes",
                table: "OrganizationSettings");

            migrationBuilder.DropColumn(
                name: "LogoUpdatedAtUtc",
                table: "OrganizationSettings");

            migrationBuilder.DropColumn(
                name: "LogoVersion",
                table: "OrganizationSettings");
        }
    }
}
