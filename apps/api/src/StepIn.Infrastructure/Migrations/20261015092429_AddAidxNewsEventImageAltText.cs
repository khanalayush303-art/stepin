using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StepIn.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAidxNewsEventImageAltText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageAltText",
                schema: "aidx",
                table: "AidxNews",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageAltText",
                schema: "aidx",
                table: "AidxEvents",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            // Honest placeholder for every row that already has an image, matching the same
            // backfill approach used for AidxResearchers.ImageAltText — never left blank (which the
            // upload form could then silently treat as "no alt text needed") and never forced
            // NOT NULL (which would break existing rows outright).
            migrationBuilder.Sql(
                """
                UPDATE aidx."AidxNews"
                SET "ImageAltText" = 'Image for ' || "Title" || ' — description pending'
                WHERE "ImageKey" IS NOT NULL AND "ImageAltText" IS NULL;
                """);

            migrationBuilder.Sql(
                """
                UPDATE aidx."AidxEvents"
                SET "ImageAltText" = 'Image for ' || "Title" || ' — description pending'
                WHERE "ImageKey" IS NOT NULL AND "ImageAltText" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageAltText",
                schema: "aidx",
                table: "AidxNews");

            migrationBuilder.DropColumn(
                name: "ImageAltText",
                schema: "aidx",
                table: "AidxEvents");
        }
    }
}
