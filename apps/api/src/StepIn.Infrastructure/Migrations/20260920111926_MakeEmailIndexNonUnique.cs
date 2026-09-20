using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StepIn.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MakeEmailIndexNonUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                schema: "stepin",
                table: "Users");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                schema: "stepin",
                table: "Users",
                column: "Email");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                schema: "stepin",
                table: "Users");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                schema: "stepin",
                table: "Users",
                column: "Email",
                unique: true);
        }
    }
}
