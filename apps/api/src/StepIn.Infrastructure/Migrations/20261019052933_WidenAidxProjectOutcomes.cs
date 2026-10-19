using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StepIn.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class WidenAidxProjectOutcomes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Outcomes",
                schema: "aidx",
                table: "AidxProjects",
                type: "character varying(20000)",
                maxLength: 20000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(512)",
                oldMaxLength: 512,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Outcomes",
                schema: "aidx",
                table: "AidxProjects",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(20000)",
                oldMaxLength: 20000,
                oldNullable: true);
        }
    }
}
