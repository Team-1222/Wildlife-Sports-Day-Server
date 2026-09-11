using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wildlife_Sports_Day_Server.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueUserNickname : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_users_nickname",
                table: "users",
                column: "nickname",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_users_nickname",
                table: "users");
        }
    }
}
