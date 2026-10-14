using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StepIn.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAidxEditorialWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AidxPublications_Doi",
                schema: "aidx",
                table: "AidxPublications");

            migrationBuilder.DropIndex(
                name: "IX_AidxProjects_Slug",
                schema: "aidx",
                table: "AidxProjects");

            migrationBuilder.DropIndex(
                name: "IX_AidxNews_Slug",
                schema: "aidx",
                table: "AidxNews");

            migrationBuilder.DropIndex(
                name: "IX_AidxEvents_Slug",
                schema: "aidx",
                table: "AidxEvents");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DiscardedAt",
                schema: "aidx",
                table: "AidxPublications",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "MergedAt",
                schema: "aidx",
                table: "AidxPublications",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PublishedAt",
                schema: "aidx",
                table: "AidxPublications",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RevisionNumber",
                schema: "aidx",
                table: "AidxPublications",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "RevisionOfId",
                schema: "aidx",
                table: "AidxPublications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                schema: "aidx",
                table: "AidxPublications",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "aidx",
                table: "AidxPublications",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DiscardedAt",
                schema: "aidx",
                table: "AidxProjects",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "MergedAt",
                schema: "aidx",
                table: "AidxProjects",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RevisionNumber",
                schema: "aidx",
                table: "AidxProjects",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "RevisionOfId",
                schema: "aidx",
                table: "AidxProjects",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "aidx",
                table: "AidxProjects",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DiscardedAt",
                schema: "aidx",
                table: "AidxNews",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "MergedAt",
                schema: "aidx",
                table: "AidxNews",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RevisionNumber",
                schema: "aidx",
                table: "AidxNews",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "RevisionOfId",
                schema: "aidx",
                table: "AidxNews",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "aidx",
                table: "AidxNews",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DiscardedAt",
                schema: "aidx",
                table: "AidxEvents",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "MergedAt",
                schema: "aidx",
                table: "AidxEvents",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RevisionNumber",
                schema: "aidx",
                table: "AidxEvents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "RevisionOfId",
                schema: "aidx",
                table: "AidxEvents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "aidx",
                table: "AidxEvents",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateTable(
                name: "AidxContentSnapshots",
                schema: "aidx",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    SnapshotNumber = table.Column<int>(type: "integer", nullable: false),
                    SubmittedByMembershipId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContentJson = table.Column<string>(type: "jsonb", maxLength: 50000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AidxContentSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AidxContentSnapshots_AidxMemberships_SubmittedByMembershipId",
                        column: x => x.SubmittedByMembershipId,
                        principalSchema: "aidx",
                        principalTable: "AidxMemberships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AidxEditorialAuditEntries",
                schema: "aidx",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    SnapshotId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PerformedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PerformedByMembershipId = table.Column<Guid>(type: "uuid", nullable: true),
                    Comment = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AidxEditorialAuditEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AidxEditorialAuditEntries_AidxContentSnapshots_SnapshotId",
                        column: x => x.SnapshotId,
                        principalSchema: "aidx",
                        principalTable: "AidxContentSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AidxEditorialAuditEntries_AidxMemberships_PerformedByMember~",
                        column: x => x.PerformedByMembershipId,
                        principalSchema: "aidx",
                        principalTable: "AidxMemberships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AidxEditorialAuditEntries_Users_PerformedByUserId",
                        column: x => x.PerformedByUserId,
                        principalSchema: "stepin",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AidxPublications_Doi",
                schema: "aidx",
                table: "AidxPublications",
                column: "Doi",
                unique: true,
                filter: "\"Doi\" IS NOT NULL AND \"RevisionOfId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AidxPublications_OneOpenRevisionPerCanonical",
                schema: "aidx",
                table: "AidxPublications",
                column: "RevisionOfId",
                unique: true,
                filter: "\"RevisionOfId\" IS NOT NULL AND \"MergedAt\" IS NULL AND \"DiscardedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AidxProjects_OneOpenRevisionPerCanonical",
                schema: "aidx",
                table: "AidxProjects",
                column: "RevisionOfId",
                unique: true,
                filter: "\"RevisionOfId\" IS NOT NULL AND \"MergedAt\" IS NULL AND \"DiscardedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AidxProjects_Slug",
                schema: "aidx",
                table: "AidxProjects",
                column: "Slug",
                unique: true,
                filter: "\"RevisionOfId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AidxNews_OneOpenRevisionPerCanonical",
                schema: "aidx",
                table: "AidxNews",
                column: "RevisionOfId",
                unique: true,
                filter: "\"RevisionOfId\" IS NOT NULL AND \"MergedAt\" IS NULL AND \"DiscardedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AidxNews_Slug",
                schema: "aidx",
                table: "AidxNews",
                column: "Slug",
                unique: true,
                filter: "\"RevisionOfId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AidxEvents_OneOpenRevisionPerCanonical",
                schema: "aidx",
                table: "AidxEvents",
                column: "RevisionOfId",
                unique: true,
                filter: "\"RevisionOfId\" IS NOT NULL AND \"MergedAt\" IS NULL AND \"DiscardedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AidxEvents_Slug",
                schema: "aidx",
                table: "AidxEvents",
                column: "Slug",
                unique: true,
                filter: "\"RevisionOfId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AidxContentSnapshots_SubjectId_SnapshotNumber",
                schema: "aidx",
                table: "AidxContentSnapshots",
                columns: new[] { "SubjectId", "SnapshotNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AidxContentSnapshots_SubmittedByMembershipId",
                schema: "aidx",
                table: "AidxContentSnapshots",
                column: "SubmittedByMembershipId");

            migrationBuilder.CreateIndex(
                name: "IX_AidxEditorialAuditEntries_ContentType_SubjectId",
                schema: "aidx",
                table: "AidxEditorialAuditEntries",
                columns: new[] { "ContentType", "SubjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_AidxEditorialAuditEntries_PerformedByMembershipId",
                schema: "aidx",
                table: "AidxEditorialAuditEntries",
                column: "PerformedByMembershipId");

            migrationBuilder.CreateIndex(
                name: "IX_AidxEditorialAuditEntries_PerformedByUserId",
                schema: "aidx",
                table: "AidxEditorialAuditEntries",
                column: "PerformedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AidxEditorialAuditEntries_SnapshotId",
                schema: "aidx",
                table: "AidxEditorialAuditEntries",
                column: "SnapshotId");

            migrationBuilder.AddForeignKey(
                name: "FK_AidxEvents_AidxEvents_RevisionOfId",
                schema: "aidx",
                table: "AidxEvents",
                column: "RevisionOfId",
                principalSchema: "aidx",
                principalTable: "AidxEvents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AidxNews_AidxNews_RevisionOfId",
                schema: "aidx",
                table: "AidxNews",
                column: "RevisionOfId",
                principalSchema: "aidx",
                principalTable: "AidxNews",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AidxProjects_AidxProjects_RevisionOfId",
                schema: "aidx",
                table: "AidxProjects",
                column: "RevisionOfId",
                principalSchema: "aidx",
                principalTable: "AidxProjects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AidxPublications_AidxPublications_RevisionOfId",
                schema: "aidx",
                table: "AidxPublications",
                column: "RevisionOfId",
                principalSchema: "aidx",
                principalTable: "AidxPublications",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // --- Data backfill (expand/backfill, not contract — see
            // docs/deployment/aidx-migration-plan.md) ---------------------------------------

            // AidxPublication.Status is new; AidxPublication.Published (the pre-existing bool)
            // is kept and dual-written by application code for the rollout window, never dropped
            // here. This is the one-time mapping from the old shape to the new: true -> Published,
            // false -> Draft. There is no prior "Archived" concept for publications, so this
            // mapping is lossless.
            migrationBuilder.Sql(
                """
                UPDATE aidx."AidxPublications"
                SET "Status" = CASE WHEN "Published" THEN 'Published' ELSE 'Draft' END
                """);

            // Every pre-existing row implicitly represents "round 1" of its own history, whether
            // or not it has ever been through this workflow — the default of 0 EF applied above
            // would otherwise make the first real edit's revision look like #1 of nothing.
            migrationBuilder.Sql("UPDATE aidx.\"AidxProjects\" SET \"RevisionNumber\" = 1");
            migrationBuilder.Sql("UPDATE aidx.\"AidxNews\" SET \"RevisionNumber\" = 1");
            migrationBuilder.Sql("UPDATE aidx.\"AidxEvents\" SET \"RevisionNumber\" = 1");
            migrationBuilder.Sql("UPDATE aidx.\"AidxPublications\" SET \"RevisionNumber\" = 1");

            // Synthetic snapshot #1 for every already-Published row, including legacy
            // admin-created ones with no membership at all (SubmittedByMembershipId left null) —
            // preserves "the original published version" as an immutable historical record from
            // before this system existed, so the first real edit produces snapshot #2, never
            // re-using #1 for something that happened earlier. CreatedAt is backdated to the
            // row's own PublishedAt/CreatedAt rather than "now", so the snapshot's own timestamp
            // honestly reflects when that version actually became public.
            migrationBuilder.Sql(
                """
                INSERT INTO aidx."AidxContentSnapshots"
                    ("Id", "ContentType", "SubjectId", "SnapshotNumber", "SubmittedByMembershipId", "ContentJson", "CreatedAt")
                SELECT gen_random_uuid(), 'Project', "Id", 1, NULL,
                    jsonb_build_object(
                        'Title', "Title", 'Slug', "Slug", 'ShortDescription', "ShortDescription",
                        'Description', "Description", 'Status', "Status", 'StartDate', "StartDate",
                        'EndDate', "EndDate", 'ExternalUrl', "ExternalUrl", 'Featured', "Featured",
                        'PublishedAt', "PublishedAt"),
                    COALESCE("PublishedAt", "CreatedAt")
                FROM aidx."AidxProjects" WHERE "Status" = 'Published'
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO aidx."AidxContentSnapshots"
                    ("Id", "ContentType", "SubjectId", "SnapshotNumber", "SubmittedByMembershipId", "ContentJson", "CreatedAt")
                SELECT gen_random_uuid(), 'News', "Id", 1, NULL,
                    jsonb_build_object(
                        'Slug', "Slug", 'Title', "Title", 'Summary', "Summary", 'Body', "Body",
                        'ImageKey', "ImageKey", 'AuthorResearcherId', "AuthorResearcherId",
                        'Status', "Status", 'PublishedAt', "PublishedAt"),
                    COALESCE("PublishedAt", "CreatedAt")
                FROM aidx."AidxNews" WHERE "Status" = 'Published'
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO aidx."AidxContentSnapshots"
                    ("Id", "ContentType", "SubjectId", "SnapshotNumber", "SubmittedByMembershipId", "ContentJson", "CreatedAt")
                SELECT gen_random_uuid(), 'Event', "Id", 1, NULL,
                    jsonb_build_object(
                        'Slug', "Slug", 'Title', "Title", 'Description', "Description",
                        'StartsAt', "StartsAt", 'EndsAt', "EndsAt", 'Location', "Location",
                        'RegistrationUrl', "RegistrationUrl", 'SpeakerName', "SpeakerName",
                        'ImageKey', "ImageKey", 'Status', "Status"),
                    "CreatedAt"
                FROM aidx."AidxEvents" WHERE "Status" = 'Published'
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO aidx."AidxContentSnapshots"
                    ("Id", "ContentType", "SubjectId", "SnapshotNumber", "SubmittedByMembershipId", "ContentJson", "CreatedAt")
                SELECT gen_random_uuid(), 'Publication', "Id", 1, NULL,
                    jsonb_build_object(
                        'Title', "Title", 'Abstract', "Abstract", 'PublicationType', "PublicationType",
                        'Venue', "Venue", 'Year', "Year", 'Doi', "Doi", 'ExternalUrl', "ExternalUrl",
                        'PdfKey', "PdfKey", 'Status', "Status", 'PublishedAt', "PublishedAt"),
                    COALESCE("PublishedAt", "CreatedAt")
                FROM aidx."AidxPublications" WHERE "Status" = 'Published'
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AidxEvents_AidxEvents_RevisionOfId",
                schema: "aidx",
                table: "AidxEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_AidxNews_AidxNews_RevisionOfId",
                schema: "aidx",
                table: "AidxNews");

            migrationBuilder.DropForeignKey(
                name: "FK_AidxProjects_AidxProjects_RevisionOfId",
                schema: "aidx",
                table: "AidxProjects");

            migrationBuilder.DropForeignKey(
                name: "FK_AidxPublications_AidxPublications_RevisionOfId",
                schema: "aidx",
                table: "AidxPublications");

            migrationBuilder.DropTable(
                name: "AidxEditorialAuditEntries",
                schema: "aidx");

            migrationBuilder.DropTable(
                name: "AidxContentSnapshots",
                schema: "aidx");

            migrationBuilder.DropIndex(
                name: "IX_AidxPublications_Doi",
                schema: "aidx",
                table: "AidxPublications");

            migrationBuilder.DropIndex(
                name: "IX_AidxPublications_OneOpenRevisionPerCanonical",
                schema: "aidx",
                table: "AidxPublications");

            migrationBuilder.DropIndex(
                name: "IX_AidxProjects_OneOpenRevisionPerCanonical",
                schema: "aidx",
                table: "AidxProjects");

            migrationBuilder.DropIndex(
                name: "IX_AidxProjects_Slug",
                schema: "aidx",
                table: "AidxProjects");

            migrationBuilder.DropIndex(
                name: "IX_AidxNews_OneOpenRevisionPerCanonical",
                schema: "aidx",
                table: "AidxNews");

            migrationBuilder.DropIndex(
                name: "IX_AidxNews_Slug",
                schema: "aidx",
                table: "AidxNews");

            migrationBuilder.DropIndex(
                name: "IX_AidxEvents_OneOpenRevisionPerCanonical",
                schema: "aidx",
                table: "AidxEvents");

            migrationBuilder.DropIndex(
                name: "IX_AidxEvents_Slug",
                schema: "aidx",
                table: "AidxEvents");

            migrationBuilder.DropColumn(
                name: "DiscardedAt",
                schema: "aidx",
                table: "AidxPublications");

            migrationBuilder.DropColumn(
                name: "MergedAt",
                schema: "aidx",
                table: "AidxPublications");

            migrationBuilder.DropColumn(
                name: "PublishedAt",
                schema: "aidx",
                table: "AidxPublications");

            migrationBuilder.DropColumn(
                name: "RevisionNumber",
                schema: "aidx",
                table: "AidxPublications");

            migrationBuilder.DropColumn(
                name: "RevisionOfId",
                schema: "aidx",
                table: "AidxPublications");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "aidx",
                table: "AidxPublications");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "aidx",
                table: "AidxPublications");

            migrationBuilder.DropColumn(
                name: "DiscardedAt",
                schema: "aidx",
                table: "AidxProjects");

            migrationBuilder.DropColumn(
                name: "MergedAt",
                schema: "aidx",
                table: "AidxProjects");

            migrationBuilder.DropColumn(
                name: "RevisionNumber",
                schema: "aidx",
                table: "AidxProjects");

            migrationBuilder.DropColumn(
                name: "RevisionOfId",
                schema: "aidx",
                table: "AidxProjects");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "aidx",
                table: "AidxProjects");

            migrationBuilder.DropColumn(
                name: "DiscardedAt",
                schema: "aidx",
                table: "AidxNews");

            migrationBuilder.DropColumn(
                name: "MergedAt",
                schema: "aidx",
                table: "AidxNews");

            migrationBuilder.DropColumn(
                name: "RevisionNumber",
                schema: "aidx",
                table: "AidxNews");

            migrationBuilder.DropColumn(
                name: "RevisionOfId",
                schema: "aidx",
                table: "AidxNews");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "aidx",
                table: "AidxNews");

            migrationBuilder.DropColumn(
                name: "DiscardedAt",
                schema: "aidx",
                table: "AidxEvents");

            migrationBuilder.DropColumn(
                name: "MergedAt",
                schema: "aidx",
                table: "AidxEvents");

            migrationBuilder.DropColumn(
                name: "RevisionNumber",
                schema: "aidx",
                table: "AidxEvents");

            migrationBuilder.DropColumn(
                name: "RevisionOfId",
                schema: "aidx",
                table: "AidxEvents");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "aidx",
                table: "AidxEvents");

            migrationBuilder.CreateIndex(
                name: "IX_AidxPublications_Doi",
                schema: "aidx",
                table: "AidxPublications",
                column: "Doi",
                unique: true,
                filter: "\"Doi\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AidxProjects_Slug",
                schema: "aidx",
                table: "AidxProjects",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AidxNews_Slug",
                schema: "aidx",
                table: "AidxNews",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AidxEvents_Slug",
                schema: "aidx",
                table: "AidxEvents",
                column: "Slug",
                unique: true);
        }
    }
}
