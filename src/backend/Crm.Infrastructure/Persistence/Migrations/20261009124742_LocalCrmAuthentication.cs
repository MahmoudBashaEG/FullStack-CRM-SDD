using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Crm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LocalCrmAuthentication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Preserve legacy identities/foreign keys/history; they are no longer new choices.
            migrationBuilder.Sql("UPDATE dbo.AgentReferences SET IsEligible=0");
            migrationBuilder.AddColumn<Guid>(
                name: "LocalUserId",
                schema: "dbo",
                table: "AgentReferences",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CrmUsers",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    AccountRole = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    AuthVersion = table.Column<long>(type: "bigint", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false, collation: "Latin1_General_100_BIN2"),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false, collation: "Latin1_General_100_BIN2"),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmUsers", x => x.Id);
                    table.CheckConstraint("CK_CrmUsers_AccountRole", "[AccountRole] IN (0,1,2)");
                    table.CheckConstraint("CK_CrmUsers_AuthVersion", "[AuthVersion]>0");
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_CrmUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "dbo",
                        principalTable: "CrmUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_CrmUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "dbo",
                        principalTable: "CrmUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_CrmUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "dbo",
                        principalTable: "CrmUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AuthenticationSessions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthVersionAtCreation = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RevocationReason = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthenticationSessions", x => x.Id);
                    table.CheckConstraint("CK_Sessions_Expiry", "[ExpiresAtUtc]>[CreatedAtUtc]");
                    table.CheckConstraint("CK_Sessions_Reason", "[RevocationReason] IS NULL OR [RevocationReason] IN (0,1,2,3)");
                    table.CheckConstraint("CK_Sessions_Version", "[AuthVersionAtCreation]>0");
                    table.ForeignKey(
                        name: "FK_AuthenticationSessions_CrmUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "dbo",
                        principalTable: "CrmUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InitialAdminSetup",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    AdminUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InitialAdminSetup", x => x.Id);
                    table.CheckConstraint("CK_AdminSetup_Singleton", "[Id]=1");
                    table.ForeignKey(
                        name: "FK_InitialAdminSetup_CrmUsers_AdminUserId",
                        column: x => x.AdminUserId,
                        principalSchema: "dbo",
                        principalTable: "CrmUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RefreshCredentials",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ConsumedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReplacedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshCredentials", x => x.Id);
                    table.CheckConstraint("CK_Refresh_Expiry", "[ExpiresAtUtc]>[CreatedAtUtc]");
                    table.ForeignKey(
                        name: "FK_RefreshCredentials_AuthenticationSessions_SessionId",
                        column: x => x.SessionId,
                        principalSchema: "dbo",
                        principalTable: "AuthenticationSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RefreshCredentials_RefreshCredentials_ReplacedById",
                        column: x => x.ReplacedById,
                        principalSchema: "dbo",
                        principalTable: "RefreshCredentials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentReferences_LocalUserId",
                schema: "dbo",
                table: "AgentReferences",
                column: "LocalUserId",
                unique: true,
                filter: "[LocalUserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuthenticationSessions_Id_UserId",
                schema: "dbo",
                table: "AuthenticationSessions",
                columns: new[] { "Id", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuthenticationSessions_UserId",
                schema: "dbo",
                table: "AuthenticationSessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmUsers_AccountRole_DisplayName_Id",
                schema: "dbo",
                table: "CrmUsers",
                columns: new[] { "AccountRole", "DisplayName", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_CrmUsers_NormalizedEmail",
                schema: "dbo",
                table: "CrmUsers",
                column: "NormalizedEmail",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CrmUsers_NormalizedUserName",
                schema: "dbo",
                table: "CrmUsers",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InitialAdminSetup_AdminUserId",
                schema: "dbo",
                table: "InitialAdminSetup",
                column: "AdminUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshCredentials_ReplacedById",
                schema: "dbo",
                table: "RefreshCredentials",
                column: "ReplacedById");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshCredentials_SessionId",
                schema: "dbo",
                table: "RefreshCredentials",
                column: "SessionId",
                unique: true,
                filter: "[ConsumedAtUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshCredentials_TokenHash",
                schema: "dbo",
                table: "RefreshCredentials",
                column: "TokenHash",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AgentReferences_CrmUsers_LocalUserId",
                schema: "dbo",
                table: "AgentReferences",
                column: "LocalUserId",
                principalSchema: "dbo",
                principalTable: "CrmUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AgentReferences_CrmUsers_LocalUserId",
                schema: "dbo",
                table: "AgentReferences");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "InitialAdminSetup",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RefreshCredentials",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AuthenticationSessions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "CrmUsers",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_AgentReferences_LocalUserId",
                schema: "dbo",
                table: "AgentReferences");

            migrationBuilder.DropColumn(
                name: "LocalUserId",
                schema: "dbo",
                table: "AgentReferences");
        }
    }
}
