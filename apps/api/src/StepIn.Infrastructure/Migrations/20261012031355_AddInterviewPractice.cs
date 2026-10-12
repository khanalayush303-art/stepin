using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StepIn.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInterviewPractice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InterviewPracticeSessions",
                schema: "stepin",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TargetRole = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterviewPracticeSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InterviewPracticeQuestions",
                schema: "stepin",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false),
                    Category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    QuestionText = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    AnswerText = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    AnsweredAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterviewPracticeQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InterviewPracticeQuestions_InterviewPracticeSessions_Sessio~",
                        column: x => x.SessionId,
                        principalSchema: "stepin",
                        principalTable: "InterviewPracticeSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InterviewPracticeQuestions_SessionId",
                schema: "stepin",
                table: "InterviewPracticeQuestions",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_InterviewPracticeQuestions_SessionId_OrderIndex",
                schema: "stepin",
                table: "InterviewPracticeQuestions",
                columns: new[] { "SessionId", "OrderIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InterviewPracticeSessions_CandidateProfileId",
                schema: "stepin",
                table: "InterviewPracticeSessions",
                column: "CandidateProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_InterviewPracticeSessions_CandidateProfileId_CreatedAt",
                schema: "stepin",
                table: "InterviewPracticeSessions",
                columns: new[] { "CandidateProfileId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InterviewPracticeQuestions",
                schema: "stepin");

            migrationBuilder.DropTable(
                name: "InterviewPracticeSessions",
                schema: "stepin");
        }
    }
}
