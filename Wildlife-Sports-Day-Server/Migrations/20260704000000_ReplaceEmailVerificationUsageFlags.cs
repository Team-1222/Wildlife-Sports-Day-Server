using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wildlife_Sports_Day_Server.Migrations;

public partial class ReplaceEmailVerificationUsageFlags : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "status",
            table: "email_verification_codes",
            type: "character varying(32)",
            maxLength: 32,
            nullable: false,
            defaultValue: "Pending");

        migrationBuilder.AddColumn<DateTime>(
            name: "unavailable_at",
            table: "email_verification_codes",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.Sql(
            """
            UPDATE email_verification_codes
            SET status = CASE
                WHEN is_used = FALSE AND is_verified = FALSE THEN 'Pending'
                WHEN is_used = FALSE AND is_verified = TRUE THEN 'Verified'
                WHEN is_used = TRUE AND is_verified = TRUE THEN 'Consumed'
                WHEN is_used = TRUE AND attempt_count >= 5 THEN 'AttemptLimitExceeded'
                WHEN is_used = TRUE AND expires_at < NOW() THEN 'Expired'
                ELSE 'Revoked'
            END,
            unavailable_at = used_at;
            """);

        migrationBuilder.DropColumn(
            name: "is_used",
            table: "email_verification_codes");

        migrationBuilder.DropColumn(
            name: "is_verified",
            table: "email_verification_codes");

        migrationBuilder.DropColumn(
            name: "used_at",
            table: "email_verification_codes");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "is_used",
            table: "email_verification_codes",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "is_verified",
            table: "email_verification_codes",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<DateTime>(
            name: "used_at",
            table: "email_verification_codes",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.Sql(
            """
            UPDATE email_verification_codes
            SET is_verified = status IN ('Verified', 'Consumed'),
                is_used = status IN ('Consumed', 'Revoked', 'Expired', 'SendFailed', 'AttemptLimitExceeded'),
                used_at = unavailable_at;
            """);

        migrationBuilder.DropColumn(
            name: "status",
            table: "email_verification_codes");

        migrationBuilder.DropColumn(
            name: "unavailable_at",
            table: "email_verification_codes");
    }
}
