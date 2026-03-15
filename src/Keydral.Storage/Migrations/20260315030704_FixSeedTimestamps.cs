using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Keydral.Storage.Migrations
{
    /// <inheritdoc />
    public partial class FixSeedTimestamps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AuthenticationTag",
                table: "Secrets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Secrets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Secrets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EncryptedDataKey",
                table: "Secrets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InitializationVector",
                table: "Secrets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KeyAuthenticationTag",
                table: "Secrets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KeyInitializationVector",
                table: "Secrets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tags",
                table: "Secrets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "DurationMs",
                table: "AuditLogs",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "AuditLogs",
                type: "uuid",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 3, 15, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 3, 15, 3, 7, 3, 909, DateTimeKind.Utc).AddTicks(699) });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 3, 15, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 3, 15, 3, 7, 3, 909, DateTimeKind.Utc).AddTicks(713) });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 3, 15, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 3, 15, 3, 7, 3, 909, DateTimeKind.Utc).AddTicks(715) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AuthenticationTag",
                table: "Secrets");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Secrets");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Secrets");

            migrationBuilder.DropColumn(
                name: "EncryptedDataKey",
                table: "Secrets");

            migrationBuilder.DropColumn(
                name: "InitializationVector",
                table: "Secrets");

            migrationBuilder.DropColumn(
                name: "KeyAuthenticationTag",
                table: "Secrets");

            migrationBuilder.DropColumn(
                name: "KeyInitializationVector",
                table: "Secrets");

            migrationBuilder.DropColumn(
                name: "Tags",
                table: "Secrets");

            migrationBuilder.DropColumn(
                name: "DurationMs",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "AuditLogs");

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 3, 15, 0, 32, 10, 810, DateTimeKind.Utc).AddTicks(9654), new DateTime(2026, 3, 15, 0, 32, 10, 810, DateTimeKind.Utc).AddTicks(9642) });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 3, 15, 0, 32, 10, 810, DateTimeKind.Utc).AddTicks(9657), new DateTime(2026, 3, 15, 0, 32, 10, 810, DateTimeKind.Utc).AddTicks(9655) });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"),
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2026, 3, 15, 0, 32, 10, 810, DateTimeKind.Utc).AddTicks(9660), new DateTime(2026, 3, 15, 0, 32, 10, 810, DateTimeKind.Utc).AddTicks(9658) });
        }
    }
}
