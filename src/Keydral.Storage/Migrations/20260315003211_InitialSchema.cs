using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Keydral.Storage.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EncryptionKeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    KeyId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EncryptedDataKey = table.Column<string>(type: "text", nullable: false),
                    Algorithm = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    InitializationVector = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RotatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EncryptionKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Policies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Rules = table.Column<string>(type: "text", nullable: false),
                    Principal = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ResourcePattern = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Effect = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Actions = table.Column<string>(type: "text", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Policies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Permissions = table.Column<string>(type: "text", nullable: false),
                    IsSystemRole = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    KeycloakId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Username = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Email = table.Column<string>(type: "text", nullable: true),
                    DisplayName = table.Column<string>(type: "text", nullable: true),
                    Groups = table.Column<string>(type: "text", nullable: true),
                    Roles = table.Column<string>(type: "text", nullable: true),
                    Metadata = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastActivityAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Secrets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    EncryptedValue = table.Column<string>(type: "text", nullable: false),
                    EncryptionKeyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentVersion = table.Column<int>(type: "integer", nullable: false),
                    Metadata = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Secrets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Secrets_EncryptionKeys_EncryptionKeyId",
                        column: x => x.EncryptionKeyId,
                        principalTable: "EncryptionKeys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ResourceType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ResourceId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ResourceName = table.Column<string>(type: "text", nullable: false),
                    Actor = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SourceIp = table.Column<string>(type: "text", nullable: true),
                    HttpMethod = table.Column<string>(type: "text", nullable: true),
                    Result = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StatusCode = table.Column<int>(type: "integer", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    UserAgent = table.Column<string>(type: "text", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RequestId = table.Column<string>(type: "text", nullable: true),
                    SecretId = table.Column<Guid>(type: "uuid", nullable: true),
                    Metadata = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditLogs_Secrets_SecretId",
                        column: x => x.SecretId,
                        principalTable: "Secrets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SecretVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SecretId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    EncryptedValue = table.Column<string>(type: "text", nullable: false),
                    EncryptionKeyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ChangeDescription = table.Column<string>(type: "text", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecretVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SecretVersions_EncryptionKeys_EncryptionKeyId",
                        column: x => x.EncryptionKeyId,
                        principalTable: "EncryptionKeys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SecretVersions_Secrets_SecretId",
                        column: x => x.SecretId,
                        principalTable: "Secrets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "CreatedAt", "CreatedBy", "Description", "IsActive", "IsSystemRole", "Name", "Permissions", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-111111111111"), new DateTime(2026, 3, 15, 0, 32, 10, 810, DateTimeKind.Utc).AddTicks(9654), "system", "Can read secret values and versions", true, true, "secret-reader", "secrets:read,audit:read", new DateTime(2026, 3, 15, 0, 32, 10, 810, DateTimeKind.Utc).AddTicks(9642) },
                    { new Guid("22222222-2222-2222-2222-222222222222"), new DateTime(2026, 3, 15, 0, 32, 10, 810, DateTimeKind.Utc).AddTicks(9657), "system", "Can read, write, and delete secrets", true, true, "secret-writer", "secrets:read,secrets:write,secrets:delete,audit:read", new DateTime(2026, 3, 15, 0, 32, 10, 810, DateTimeKind.Utc).AddTicks(9655) },
                    { new Guid("33333333-3333-3333-3333-333333333333"), new DateTime(2026, 3, 15, 0, 32, 10, 810, DateTimeKind.Utc).AddTicks(9660), "system", "Full administrator access to secrets, policies, and audit", true, true, "secret-admin", "secrets:*,policies:*,users:*,roles:*,audit:*,encryption:*", new DateTime(2026, 3, 15, 0, 32, 10, 810, DateTimeKind.Utc).AddTicks(9658) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Action",
                table: "AuditLogs",
                column: "Action");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Actor",
                table: "AuditLogs",
                column: "Actor");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ResourceId",
                table: "AuditLogs",
                column: "ResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Result",
                table: "AuditLogs",
                column: "Result");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_SecretId",
                table: "AuditLogs",
                column: "SecretId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Timestamp",
                table: "AuditLogs",
                column: "Timestamp",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Timestamp_Result",
                table: "AuditLogs",
                columns: new[] { "Timestamp", "Result" });

            migrationBuilder.CreateIndex(
                name: "IX_EncryptionKeys_CreatedAt",
                table: "EncryptionKeys",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_EncryptionKeys_IsActive",
                table: "EncryptionKeys",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_EncryptionKeys_KeyId",
                table: "EncryptionKeys",
                column: "KeyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Policies_IsDeleted",
                table: "Policies",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_Policies_IsEnabled",
                table: "Policies",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_Policies_Principal",
                table: "Policies",
                column: "Principal");

            migrationBuilder.CreateIndex(
                name: "IX_Policies_ResourcePattern",
                table: "Policies",
                column: "ResourcePattern");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_IsActive",
                table: "Roles",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_IsSystemRole",
                table: "Roles",
                column: "IsSystemRole");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Name",
                table: "Roles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Secrets_CreatedAt",
                table: "Secrets",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Secrets_EncryptionKeyId",
                table: "Secrets",
                column: "EncryptionKeyId");

            migrationBuilder.CreateIndex(
                name: "IX_Secrets_IsDeleted",
                table: "Secrets",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_Secrets_Name",
                table: "Secrets",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Secrets_UpdatedAt",
                table: "Secrets",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SecretVersions_CreatedAt",
                table: "SecretVersions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SecretVersions_EncryptionKeyId",
                table: "SecretVersions",
                column: "EncryptionKeyId");

            migrationBuilder.CreateIndex(
                name: "IX_SecretVersions_SecretId",
                table: "SecretVersions",
                column: "SecretId");

            migrationBuilder.CreateIndex(
                name: "IX_SecretVersions_SecretId_VersionNumber",
                table: "SecretVersions",
                columns: new[] { "SecretId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_IsActive",
                table: "Users",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Users_KeycloakId",
                table: "Users",
                column: "KeycloakId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_LastActivityAt",
                table: "Users",
                column: "LastActivityAt");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "Policies");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "SecretVersions");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Secrets");

            migrationBuilder.DropTable(
                name: "EncryptionKeys");
        }
    }
}
