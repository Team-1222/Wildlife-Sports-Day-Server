using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Wildlife_Sports_Day_Server.Migrations;

public partial class AddEmailVerificationRegister : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "email_verification_codes",
            columns: table => new
            {
                email_verification_code_id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                code_hash = table.Column<string>(type: "text", nullable: false),
                expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                attempt_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                is_verified = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                is_used = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                verified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                used_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_email_verification_codes", x => x.email_verification_code_id);
            });

        migrationBuilder.CreateTable(
            name: "users",
            columns: table => new
            {
                user_id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                nickname = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                password_hash = table.Column<string>(type: "text", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_users", x => x.user_id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_email_verification_codes_email",
            table: "email_verification_codes",
            column: "email");

        migrationBuilder.CreateIndex(
            name: "IX_users_email",
            table: "users",
            column: "email",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "email_verification_codes");

        migrationBuilder.DropTable(
            name: "users");
    }
}
