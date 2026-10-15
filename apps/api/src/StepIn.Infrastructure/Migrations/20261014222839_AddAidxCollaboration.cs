using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StepIn.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAidxCollaboration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Outcomes",
                schema: "aidx",
                table: "AidxProjects",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Capacity",
                schema: "aidx",
                table: "AidxEvents",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AidxCollaborationEnquiries",
                schema: "aidx",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Message = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AidxCollaborationEnquiries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AidxCollaborationEnquiries_AidxProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "aidx",
                        principalTable: "AidxProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AidxEventRegistrations",
                schema: "aidx",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CancellationTokenHash = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AidxEventRegistrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AidxEventRegistrations_AidxEvents_EventId",
                        column: x => x.EventId,
                        principalSchema: "aidx",
                        principalTable: "AidxEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AidxEventRegistrations_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "stepin",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AidxProjectMilestones",
                schema: "aidx",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    TargetDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CompletedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AidxProjectMilestones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AidxProjectMilestones_AidxProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "aidx",
                        principalTable: "AidxProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AidxSavedItems",
                schema: "aidx",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ContentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AidxSavedItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AidxSavedItems_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "stepin",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AidxCollaborationEnquiries_ProjectId",
                schema: "aidx",
                table: "AidxCollaborationEnquiries",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_AidxEventRegistrations_EventId_Email",
                schema: "aidx",
                table: "AidxEventRegistrations",
                columns: new[] { "EventId", "Email" },
                unique: true,
                filter: "\"UserId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AidxEventRegistrations_EventId_UserId",
                schema: "aidx",
                table: "AidxEventRegistrations",
                columns: new[] { "EventId", "UserId" },
                unique: true,
                filter: "\"UserId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AidxEventRegistrations_UserId",
                schema: "aidx",
                table: "AidxEventRegistrations",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AidxProjectMilestones_ProjectId",
                schema: "aidx",
                table: "AidxProjectMilestones",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_AidxSavedItems_UserId_ContentType_ContentId",
                schema: "aidx",
                table: "AidxSavedItems",
                columns: new[] { "UserId", "ContentType", "ContentId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AidxCollaborationEnquiries",
                schema: "aidx");

            migrationBuilder.DropTable(
                name: "AidxEventRegistrations",
                schema: "aidx");

            migrationBuilder.DropTable(
                name: "AidxProjectMilestones",
                schema: "aidx");

            migrationBuilder.DropTable(
                name: "AidxSavedItems",
                schema: "aidx");

            migrationBuilder.DropColumn(
                name: "Outcomes",
                schema: "aidx",
                table: "AidxProjects");

            migrationBuilder.DropColumn(
                name: "Capacity",
                schema: "aidx",
                table: "AidxEvents");
        }
    }
}
