using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StepIn.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAidxResearcherImageAltText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageAltText",
                schema: "aidx",
                table: "AidxResearchers",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            // Honest placeholder for every row that already has a photo, rather than left blank
            // (which the upload form could then silently treat as "no alt text needed") or forced
            // NOT NULL (which would break existing rows outright). "Required when an image is set"
            // is enforced going forward at the application-validation layer, the same way every
            // other "required-when" rule in this codebase already works.
            migrationBuilder.Sql(
                """
                UPDATE aidx."AidxResearchers"
                SET "ImageAltText" = 'Photo of ' || "DisplayName" || ' — description pending'
                WHERE "ProfileImageKey" IS NOT NULL AND "ImageAltText" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageAltText",
                schema: "aidx",
                table: "AidxResearchers");
        }
    }
}
