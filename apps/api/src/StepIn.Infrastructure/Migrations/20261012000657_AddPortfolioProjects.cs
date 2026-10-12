using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StepIn.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPortfolioProjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PortfolioProjects",
                schema: "stepin",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    Skills = table.Column<string[]>(type: "text[]", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    RepositoryUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DemoUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Visibility = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ResearchProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortfolioProjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PortfolioProjects_AidxProjects_ResearchProjectId",
                        column: x => x.ResearchProjectId,
                        principalSchema: "aidx",
                        principalTable: "AidxProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioProjects_CandidateProfileId",
                schema: "stepin",
                table: "PortfolioProjects",
                column: "CandidateProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioProjects_CandidateProfileId_Visibility",
                schema: "stepin",
                table: "PortfolioProjects",
                columns: new[] { "CandidateProfileId", "Visibility" });

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioProjects_ResearchProjectId",
                schema: "stepin",
                table: "PortfolioProjects",
                column: "ResearchProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PortfolioProjects",
                schema: "stepin");
        }
    }
}
