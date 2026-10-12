using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StepIn.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExperiencePassport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExperienceRecords",
                schema: "stepin",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Organization = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Skills = table.Column<string[]>(type: "text[]", nullable: false),
                    EvidenceUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Visibility = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExperienceRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExperienceVerificationAudits",
                schema: "stepin",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExperienceRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorRole = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PreviousStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NewStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExperienceVerificationAudits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExperienceVerificationAudits_ExperienceRecords_ExperienceRe~",
                        column: x => x.ExperienceRecordId,
                        principalSchema: "stepin",
                        principalTable: "ExperienceRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExperienceRecords_CandidateProfileId",
                schema: "stepin",
                table: "ExperienceRecords",
                column: "CandidateProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ExperienceRecords_CandidateProfileId_CreatedAt",
                schema: "stepin",
                table: "ExperienceRecords",
                columns: new[] { "CandidateProfileId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ExperienceRecords_CandidateProfileId_Status",
                schema: "stepin",
                table: "ExperienceRecords",
                columns: new[] { "CandidateProfileId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ExperienceRecords_Status_SubmittedAt",
                schema: "stepin",
                table: "ExperienceRecords",
                columns: new[] { "Status", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ExperienceVerificationAudits_ExperienceRecordId_CreatedAt",
                schema: "stepin",
                table: "ExperienceVerificationAudits",
                columns: new[] { "ExperienceRecordId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExperienceVerificationAudits",
                schema: "stepin");

            migrationBuilder.DropTable(
                name: "ExperienceRecords",
                schema: "stepin");
        }
    }
}
