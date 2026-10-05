using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StepIn.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAidxFoundations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "aidx");

            migrationBuilder.AddColumn<Guid>(
                name: "AidxProjectId",
                schema: "stepin",
                table: "Jobs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Category",
                schema: "stepin",
                table: "Jobs",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Career");

            migrationBuilder.CreateTable(
                name: "AidxEvents",
                schema: "aidx",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    Location = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    RegistrationUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SpeakerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ImageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AidxEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AidxProjects",
                schema: "aidx",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    ShortDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ExternalUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Featured = table.Column<bool>(type: "boolean", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AidxProjects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AidxPublications",
                schema: "aidx",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    Abstract = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    PublicationType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Venue = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Doi = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ExternalUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PdfKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Published = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AidxPublications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AidxResearchAreas",
                schema: "aidx",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Slug = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AidxResearchAreas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AidxResearchers",
                schema: "aidx",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Position = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Biography = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    ProfileImageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OrcidUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    GoogleScholarUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LinkedInUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    WebsiteUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Published = table.Column<bool>(type: "boolean", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AidxResearchers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AidxResearchers_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "stepin",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AidxProjectTechnologies",
                schema: "aidx",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AidxProjectTechnologies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AidxProjectTechnologies_AidxProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "aidx",
                        principalTable: "AidxProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AidxPublicationProjects",
                schema: "aidx",
                columns: table => new
                {
                    PublicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AidxPublicationProjects", x => new { x.PublicationId, x.ProjectId });
                    table.ForeignKey(
                        name: "FK_AidxPublicationProjects_AidxProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "aidx",
                        principalTable: "AidxProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AidxPublicationProjects_AidxPublications_PublicationId",
                        column: x => x.PublicationId,
                        principalSchema: "aidx",
                        principalTable: "AidxPublications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AidxProjectResearchAreas",
                schema: "aidx",
                columns: table => new
                {
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResearchAreaId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AidxProjectResearchAreas", x => new { x.ProjectId, x.ResearchAreaId });
                    table.ForeignKey(
                        name: "FK_AidxProjectResearchAreas_AidxProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "aidx",
                        principalTable: "AidxProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AidxProjectResearchAreas_AidxResearchAreas_ResearchAreaId",
                        column: x => x.ResearchAreaId,
                        principalSchema: "aidx",
                        principalTable: "AidxResearchAreas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AidxPublicationResearchAreas",
                schema: "aidx",
                columns: table => new
                {
                    PublicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResearchAreaId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AidxPublicationResearchAreas", x => new { x.PublicationId, x.ResearchAreaId });
                    table.ForeignKey(
                        name: "FK_AidxPublicationResearchAreas_AidxPublications_PublicationId",
                        column: x => x.PublicationId,
                        principalSchema: "aidx",
                        principalTable: "AidxPublications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AidxPublicationResearchAreas_AidxResearchAreas_ResearchArea~",
                        column: x => x.ResearchAreaId,
                        principalSchema: "aidx",
                        principalTable: "AidxResearchAreas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AidxNews",
                schema: "aidx",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Body = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    ImageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AuthorResearcherId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AidxNews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AidxNews_AidxResearchers_AuthorResearcherId",
                        column: x => x.AuthorResearcherId,
                        principalSchema: "aidx",
                        principalTable: "AidxResearchers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AidxProjectResearchers",
                schema: "aidx",
                columns: table => new
                {
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResearcherId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AidxProjectResearchers", x => new { x.ProjectId, x.ResearcherId });
                    table.ForeignKey(
                        name: "FK_AidxProjectResearchers_AidxProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "aidx",
                        principalTable: "AidxProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AidxProjectResearchers_AidxResearchers_ResearcherId",
                        column: x => x.ResearcherId,
                        principalSchema: "aidx",
                        principalTable: "AidxResearchers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AidxPublicationAuthors",
                schema: "aidx",
                columns: table => new
                {
                    PublicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    ResearcherId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExternalAuthorName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AidxPublicationAuthors", x => new { x.PublicationId, x.Position });
                    table.ForeignKey(
                        name: "FK_AidxPublicationAuthors_AidxPublications_PublicationId",
                        column: x => x.PublicationId,
                        principalSchema: "aidx",
                        principalTable: "AidxPublications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AidxPublicationAuthors_AidxResearchers_ResearcherId",
                        column: x => x.ResearcherId,
                        principalSchema: "aidx",
                        principalTable: "AidxResearchers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_AidxProjectId",
                schema: "stepin",
                table: "Jobs",
                column: "AidxProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_Category_Status_PublishedAt",
                schema: "stepin",
                table: "Jobs",
                columns: new[] { "Category", "Status", "PublishedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AidxEvents_Slug",
                schema: "aidx",
                table: "AidxEvents",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AidxEvents_Status_StartsAt",
                schema: "aidx",
                table: "AidxEvents",
                columns: new[] { "Status", "StartsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AidxNews_AuthorResearcherId",
                schema: "aidx",
                table: "AidxNews",
                column: "AuthorResearcherId");

            migrationBuilder.CreateIndex(
                name: "IX_AidxNews_Slug",
                schema: "aidx",
                table: "AidxNews",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AidxNews_Status_PublishedAt",
                schema: "aidx",
                table: "AidxNews",
                columns: new[] { "Status", "PublishedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AidxProjectResearchAreas_ResearchAreaId",
                schema: "aidx",
                table: "AidxProjectResearchAreas",
                column: "ResearchAreaId");

            migrationBuilder.CreateIndex(
                name: "IX_AidxProjectResearchers_ResearcherId",
                schema: "aidx",
                table: "AidxProjectResearchers",
                column: "ResearcherId");

            migrationBuilder.CreateIndex(
                name: "IX_AidxProjects_Slug",
                schema: "aidx",
                table: "AidxProjects",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AidxProjects_Status_PublishedAt",
                schema: "aidx",
                table: "AidxProjects",
                columns: new[] { "Status", "PublishedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AidxProjectTechnologies_ProjectId_Name",
                schema: "aidx",
                table: "AidxProjectTechnologies",
                columns: new[] { "ProjectId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AidxPublicationAuthors_ResearcherId",
                schema: "aidx",
                table: "AidxPublicationAuthors",
                column: "ResearcherId");

            migrationBuilder.CreateIndex(
                name: "IX_AidxPublicationProjects_ProjectId",
                schema: "aidx",
                table: "AidxPublicationProjects",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_AidxPublicationResearchAreas_ResearchAreaId",
                schema: "aidx",
                table: "AidxPublicationResearchAreas",
                column: "ResearchAreaId");

            migrationBuilder.CreateIndex(
                name: "IX_AidxPublications_Doi",
                schema: "aidx",
                table: "AidxPublications",
                column: "Doi",
                unique: true,
                filter: "\"Doi\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AidxPublications_Published_Year",
                schema: "aidx",
                table: "AidxPublications",
                columns: new[] { "Published", "Year" });

            migrationBuilder.CreateIndex(
                name: "IX_AidxResearchAreas_Name",
                schema: "aidx",
                table: "AidxResearchAreas",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AidxResearchAreas_Slug",
                schema: "aidx",
                table: "AidxResearchAreas",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AidxResearchers_Slug",
                schema: "aidx",
                table: "AidxResearchers",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AidxResearchers_UserId",
                schema: "aidx",
                table: "AidxResearchers",
                column: "UserId",
                unique: true,
                filter: "\"UserId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Jobs_AidxProjects_AidxProjectId",
                schema: "stepin",
                table: "Jobs",
                column: "AidxProjectId",
                principalSchema: "aidx",
                principalTable: "AidxProjects",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Jobs_AidxProjects_AidxProjectId",
                schema: "stepin",
                table: "Jobs");

            migrationBuilder.DropTable(
                name: "AidxEvents",
                schema: "aidx");

            migrationBuilder.DropTable(
                name: "AidxNews",
                schema: "aidx");

            migrationBuilder.DropTable(
                name: "AidxProjectResearchAreas",
                schema: "aidx");

            migrationBuilder.DropTable(
                name: "AidxProjectResearchers",
                schema: "aidx");

            migrationBuilder.DropTable(
                name: "AidxProjectTechnologies",
                schema: "aidx");

            migrationBuilder.DropTable(
                name: "AidxPublicationAuthors",
                schema: "aidx");

            migrationBuilder.DropTable(
                name: "AidxPublicationProjects",
                schema: "aidx");

            migrationBuilder.DropTable(
                name: "AidxPublicationResearchAreas",
                schema: "aidx");

            migrationBuilder.DropTable(
                name: "AidxResearchers",
                schema: "aidx");

            migrationBuilder.DropTable(
                name: "AidxProjects",
                schema: "aidx");

            migrationBuilder.DropTable(
                name: "AidxPublications",
                schema: "aidx");

            migrationBuilder.DropTable(
                name: "AidxResearchAreas",
                schema: "aidx");

            migrationBuilder.DropIndex(
                name: "IX_Jobs_AidxProjectId",
                schema: "stepin",
                table: "Jobs");

            migrationBuilder.DropIndex(
                name: "IX_Jobs_Category_Status_PublishedAt",
                schema: "stepin",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "AidxProjectId",
                schema: "stepin",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "Category",
                schema: "stepin",
                table: "Jobs");
        }
    }
}
