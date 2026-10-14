using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StepIn.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAidxMembership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByMembershipId",
                schema: "aidx",
                table: "AidxPublications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByMembershipId",
                schema: "aidx",
                table: "AidxProjects",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByMembershipId",
                schema: "aidx",
                table: "AidxNews",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByMembershipId",
                schema: "aidx",
                table: "AidxEvents",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AidxMemberships",
                schema: "aidx",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ScopedResearchAreaId = table.Column<Guid>(type: "uuid", nullable: true),
                    GrantedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AidxMemberships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AidxMemberships_AidxResearchAreas_ScopedResearchAreaId",
                        column: x => x.ScopedResearchAreaId,
                        principalSchema: "aidx",
                        principalTable: "AidxResearchAreas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AidxMemberships_Users_GrantedByUserId",
                        column: x => x.GrantedByUserId,
                        principalSchema: "stepin",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AidxMemberships_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "stepin",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AidxMembershipAuditEntries",
                schema: "aidx",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MembershipId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PerformedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousRole = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    NewRole = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    PreviousScopeId = table.Column<Guid>(type: "uuid", nullable: true),
                    NewScopeId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AidxMembershipAuditEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AidxMembershipAuditEntries_AidxMemberships_MembershipId",
                        column: x => x.MembershipId,
                        principalSchema: "aidx",
                        principalTable: "AidxMemberships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AidxMembershipAuditEntries_Users_PerformedByUserId",
                        column: x => x.PerformedByUserId,
                        principalSchema: "stepin",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AidxPublications_CreatedByMembershipId",
                schema: "aidx",
                table: "AidxPublications",
                column: "CreatedByMembershipId");

            migrationBuilder.CreateIndex(
                name: "IX_AidxProjects_CreatedByMembershipId",
                schema: "aidx",
                table: "AidxProjects",
                column: "CreatedByMembershipId");

            migrationBuilder.CreateIndex(
                name: "IX_AidxNews_CreatedByMembershipId",
                schema: "aidx",
                table: "AidxNews",
                column: "CreatedByMembershipId");

            migrationBuilder.CreateIndex(
                name: "IX_AidxEvents_CreatedByMembershipId",
                schema: "aidx",
                table: "AidxEvents",
                column: "CreatedByMembershipId");

            migrationBuilder.CreateIndex(
                name: "IX_AidxMembershipAuditEntries_MembershipId",
                schema: "aidx",
                table: "AidxMembershipAuditEntries",
                column: "MembershipId");

            migrationBuilder.CreateIndex(
                name: "IX_AidxMembershipAuditEntries_PerformedByUserId",
                schema: "aidx",
                table: "AidxMembershipAuditEntries",
                column: "PerformedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AidxMemberships_GrantedByUserId",
                schema: "aidx",
                table: "AidxMemberships",
                column: "GrantedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AidxMemberships_ScopedResearchAreaId",
                schema: "aidx",
                table: "AidxMemberships",
                column: "ScopedResearchAreaId");

            migrationBuilder.CreateIndex(
                name: "IX_AidxMemberships_UserId",
                schema: "aidx",
                table: "AidxMemberships",
                column: "UserId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AidxEvents_AidxMemberships_CreatedByMembershipId",
                schema: "aidx",
                table: "AidxEvents",
                column: "CreatedByMembershipId",
                principalSchema: "aidx",
                principalTable: "AidxMemberships",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_AidxNews_AidxMemberships_CreatedByMembershipId",
                schema: "aidx",
                table: "AidxNews",
                column: "CreatedByMembershipId",
                principalSchema: "aidx",
                principalTable: "AidxMemberships",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_AidxProjects_AidxMemberships_CreatedByMembershipId",
                schema: "aidx",
                table: "AidxProjects",
                column: "CreatedByMembershipId",
                principalSchema: "aidx",
                principalTable: "AidxMemberships",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_AidxPublications_AidxMemberships_CreatedByMembershipId",
                schema: "aidx",
                table: "AidxPublications",
                column: "CreatedByMembershipId",
                principalSchema: "aidx",
                principalTable: "AidxMemberships",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AidxEvents_AidxMemberships_CreatedByMembershipId",
                schema: "aidx",
                table: "AidxEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_AidxNews_AidxMemberships_CreatedByMembershipId",
                schema: "aidx",
                table: "AidxNews");

            migrationBuilder.DropForeignKey(
                name: "FK_AidxProjects_AidxMemberships_CreatedByMembershipId",
                schema: "aidx",
                table: "AidxProjects");

            migrationBuilder.DropForeignKey(
                name: "FK_AidxPublications_AidxMemberships_CreatedByMembershipId",
                schema: "aidx",
                table: "AidxPublications");

            migrationBuilder.DropTable(
                name: "AidxMembershipAuditEntries",
                schema: "aidx");

            migrationBuilder.DropTable(
                name: "AidxMemberships",
                schema: "aidx");

            migrationBuilder.DropIndex(
                name: "IX_AidxPublications_CreatedByMembershipId",
                schema: "aidx",
                table: "AidxPublications");

            migrationBuilder.DropIndex(
                name: "IX_AidxProjects_CreatedByMembershipId",
                schema: "aidx",
                table: "AidxProjects");

            migrationBuilder.DropIndex(
                name: "IX_AidxNews_CreatedByMembershipId",
                schema: "aidx",
                table: "AidxNews");

            migrationBuilder.DropIndex(
                name: "IX_AidxEvents_CreatedByMembershipId",
                schema: "aidx",
                table: "AidxEvents");

            migrationBuilder.DropColumn(
                name: "CreatedByMembershipId",
                schema: "aidx",
                table: "AidxPublications");

            migrationBuilder.DropColumn(
                name: "CreatedByMembershipId",
                schema: "aidx",
                table: "AidxProjects");

            migrationBuilder.DropColumn(
                name: "CreatedByMembershipId",
                schema: "aidx",
                table: "AidxNews");

            migrationBuilder.DropColumn(
                name: "CreatedByMembershipId",
                schema: "aidx",
                table: "AidxEvents");
        }
    }
}
