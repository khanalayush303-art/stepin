using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StepIn.Domain.Aidx;
using StepIn.Infrastructure.Persistence;

namespace StepIn.Infrastructure.Aidx;

/// <summary>
/// The one and only path that mutates AIDX editorial-workflow content
/// (<see cref="AidxProject"/>, <see cref="AidxNews"/>, <see cref="AidxEvent"/>,
/// <see cref="AidxPublication"/>) — both the old <c>AidxAdminEndpoints</c> and
/// the new membership-gated endpoints call through here, so there is
/// structurally one place to apply a transition, not several to keep in sync.
/// Generic over <see cref="IAidxWorkflowContent"/> so the state machine,
/// snapshotting, concurrency, and revision/merge mechanics are implemented
/// once rather than four near-duplicate times.
///
/// Authorization (who may invoke a given action) is deliberately NOT checked
/// here — callers check <c>AidxPermissions</c> first, matching every other
/// endpoint family in this codebase. This class only knows whether a
/// transition is structurally valid and applies its mechanics correctly.
/// </summary>
public sealed class AidxContentEditingService(ApplicationDbContext db)
{
    /// <summary>Reads a content row's current optimistic-concurrency token (the xmin shadow
    /// property) as a string, for returning to a client alongside a GET-for-edit response.</summary>
    public string GetVersion<TContent>(TContent content) where TContent : class, IAidxWorkflowContent =>
        db.Entry(content).Property<uint>("xmin").CurrentValue.ToString();

    private bool VersionMatches<TContent>(TContent content, string expectedVersion) where TContent : class, IAidxWorkflowContent =>
        string.Equals(GetVersion(content), expectedVersion, StringComparison.Ordinal);

    /// <summary>
    /// Returns the row the caller should actually mutate: the canonical row itself if it's
    /// still pre-first-publish (Draft/ChangesRequested with no prior Published state), or a
    /// find-or-create open revision if the canonical row is already Published. Never mutates
    /// a Published canonical row directly — that's the entire point of this indirection.
    /// </summary>
    /// <param name="set">The content type's own DbSet.</param>
    /// <param name="canonical">The canonical (non-revision) row to begin editing.</param>
    /// <param name="reparentChildren">For content types with owned nav collections (e.g.
    /// AidxProject.ResearchAreas), copies and re-points each child row's foreign key at the new
    /// revision's id. Omit for content types with no such collections.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<TContent> BeginEditAsync<TContent>(
        DbSet<TContent> set,
        TContent canonical,
        Action<TContent, TContent>? reparentChildren,
        CancellationToken cancellationToken)
        where TContent : class, IAidxWorkflowContent
    {
        if (canonical.Status != AidxContentStatus.Published)
        {
            return canonical; // still pre-first-publish — edited in place
        }

        var openRevision = await set
            .FirstOrDefaultAsync(
                c => EF.Property<Guid?>(c, nameof(IAidxWorkflowContent.RevisionOfId)) == canonical.Id
                     && c.MergedAt == null && c.DiscardedAt == null,
                cancellationToken);

        if (openRevision is not null)
        {
            return openRevision;
        }

        var json = JsonSerializer.Serialize(canonical, canonical.GetType());
        var clone = (TContent)JsonSerializer.Deserialize(json, canonical.GetType())!;
        clone.RevisionOfId = canonical.Id;
        clone.Status = AidxContentStatus.Draft;
        clone.MergedAt = null;
        clone.DiscardedAt = null;

        var nextRevisionNumber = await set
            .Where(c => EF.Property<Guid?>(c, nameof(IAidxWorkflowContent.RevisionOfId)) == canonical.Id)
            .Select(c => (int?)c.RevisionNumber)
            .MaxAsync(cancellationToken) ?? 1; // canonical's own pre-publish round is implicitly #1
        clone.RevisionNumber = nextRevisionNumber + 1;

        set.Add(clone);
        await db.SaveChangesAsync(cancellationToken); // clone now has a real Id

        reparentChildren?.Invoke(clone, canonical);
        await db.SaveChangesAsync(cancellationToken);

        return clone;
    }

    /// <summary>Draft/ChangesRequested → Submitted. Takes an immutable snapshot, numbered
    /// atomically under a transaction-scoped advisory lock keyed to this subject row.</summary>
    public async Task<AidxEditOutcome> SubmitAsync<TContent>(
        TContent content,
        string expectedVersion,
        AidxContentType contentType,
        Guid actingUserId,
        Guid? actingMembershipId,
        CancellationToken cancellationToken)
        where TContent : class, IAidxWorkflowContent
    {
        if (!AidxEditorialWorkflow.CanTransition(content.Status, AidxContentStatus.Submitted))
        {
            return AidxEditOutcome.InvalidTransition;
        }

        if (!VersionMatches(content, expectedVersion))
        {
            return AidxEditOutcome.VersionConflict;
        }

        var strategy = db.Database.CreateExecutionStrategy();
        try
        {
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT pg_advisory_xact_lock(hashtext({"aidx_snapshot_" + content.Id}))", cancellationToken);

                var nextNumber = (await db.AidxContentSnapshots
                    .Where(s => s.SubjectId == content.Id)
                    .Select(s => (int?)s.SnapshotNumber)
                    .MaxAsync(cancellationToken) ?? 0) + 1;

                content.Status = AidxContentStatus.Submitted;
                var json = JsonSerializer.Serialize(content, content.GetType());

                var snapshot = new AidxContentSnapshot
                {
                    ContentType = contentType,
                    SubjectId = content.Id,
                    SnapshotNumber = nextNumber,
                    SubmittedByMembershipId = actingMembershipId,
                    ContentJson = json,
                };
                db.AidxContentSnapshots.Add(snapshot);
                await db.SaveChangesAsync(cancellationToken);

                db.AidxEditorialAuditEntries.Add(new AidxEditorialAuditEntry
                {
                    ContentType = contentType,
                    SubjectId = content.Id,
                    SnapshotId = snapshot.Id,
                    Action = AidxEditorialAction.Submitted,
                    PerformedByUserId = actingUserId,
                    PerformedByMembershipId = actingMembershipId,
                });
                await db.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            // The true check-to-save race: another request's write landed between our pre-check
            // (above) and this one's save, inside the lock. Defense-in-depth for the window the
            // manual expectedVersion check alone can't close.
            return AidxEditOutcome.VersionConflict;
        }

        return AidxEditOutcome.Success;
    }

    public async Task<AidxEditOutcome> MoveToUnderReviewAsync<TContent>(
        TContent content, string expectedVersion, AidxContentType contentType,
        Guid actingUserId, Guid? actingMembershipId, CancellationToken cancellationToken)
        where TContent : class, IAidxWorkflowContent
    {
        return await TransitionAsync(
            content, expectedVersion, AidxContentStatus.UnderReview, contentType,
            AidxEditorialAction.MovedToUnderReview, actingUserId, actingMembershipId, comment: null, cancellationToken);
    }

    public async Task<AidxEditOutcome> RequestChangesAsync<TContent>(
        TContent content, string expectedVersion, AidxContentType contentType,
        Guid actingUserId, Guid? actingMembershipId, string comment, CancellationToken cancellationToken)
        where TContent : class, IAidxWorkflowContent
    {
        return await TransitionAsync(
            content, expectedVersion, AidxContentStatus.ChangesRequested, contentType,
            AidxEditorialAction.ChangesRequested, actingUserId, actingMembershipId, comment, cancellationToken);
    }

    /// <summary>Submitted/ChangesRequested → Draft. No snapshot — withdrawing is not a review
    /// outcome, it's the author taking the row back for further editing.</summary>
    public async Task<AidxEditOutcome> WithdrawAsync<TContent>(
        TContent content, string expectedVersion, AidxContentType contentType,
        Guid actingUserId, Guid? actingMembershipId, CancellationToken cancellationToken)
        where TContent : class, IAidxWorkflowContent
    {
        return await TransitionAsync(
            content, expectedVersion, AidxContentStatus.Draft, contentType,
            AidxEditorialAction.Withdrawn, actingUserId, actingMembershipId, comment: null, cancellationToken);
    }

    /// <summary>
    /// Submitted/UnderReview → Published. If <paramref name="content"/> is a canonical row
    /// (RevisionOfId is null), this is its first-ever publish: published in place. If it's a
    /// revision, its fields are copied onto <paramref name="canonical"/> in the same
    /// transaction (same Id/Slug — stable URLs), the revision is marked MergedAt, and the
    /// revision itself is never deleted.
    /// </summary>
    public async Task<AidxEditOutcome> ApproveAsync<TContent>(
        TContent content,
        TContent? canonical,
        string expectedVersion,
        AidxContentType contentType,
        Action<TContent, TContent>? copyOntoCanonical,
        Guid actingUserId,
        Guid? actingMembershipId,
        CancellationToken cancellationToken)
        where TContent : class, IAidxWorkflowContent
    {
        if (!AidxEditorialWorkflow.CanTransition(content.Status, AidxContentStatus.Published))
        {
            return AidxEditOutcome.InvalidTransition;
        }

        if (!VersionMatches(content, expectedVersion))
        {
            return AidxEditOutcome.VersionConflict;
        }

        var latestSnapshot = await db.AidxContentSnapshots
            .Where(s => s.SubjectId == content.Id)
            .OrderByDescending(s => s.SnapshotNumber)
            .FirstOrDefaultAsync(cancellationToken);

        if (content.RevisionOfId is null)
        {
            // First-ever publish: in place.
            content.Status = AidxContentStatus.Published;
            content.MergedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            if (canonical is null)
            {
                throw new InvalidOperationException("Approving a revision requires its canonical row.");
            }

            copyOntoCanonical?.Invoke(content, canonical);
            canonical.Status = AidxContentStatus.Published;
            content.Status = AidxContentStatus.Published; // the revision's own terminal state
            content.MergedAt = DateTimeOffset.UtcNow;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return AidxEditOutcome.VersionConflict;
        }

        db.AidxEditorialAuditEntries.Add(new AidxEditorialAuditEntry
        {
            ContentType = contentType,
            SubjectId = content.Id,
            SnapshotId = latestSnapshot?.Id,
            Action = AidxEditorialAction.Published,
            PerformedByUserId = actingUserId,
            PerformedByMembershipId = actingMembershipId,
        });
        await db.SaveChangesAsync(cancellationToken);

        return AidxEditOutcome.Success;
    }

    public async Task<AidxEditOutcome> ArchiveAsync<TContent>(
        TContent content, string expectedVersion, AidxContentType contentType,
        Guid actingUserId, Guid? actingMembershipId, CancellationToken cancellationToken)
        where TContent : class, IAidxWorkflowContent
    {
        return await TransitionAsync(
            content, expectedVersion, AidxContentStatus.Archived, contentType,
            AidxEditorialAction.Archived, actingUserId, actingMembershipId, comment: null, cancellationToken);
    }

    /// <summary>Abandons an open revision permanently — distinct from Withdraw, which keeps it
    /// editable. Never deletes the row; it remains a permanent (if unused) historical snapshot.</summary>
    public async Task<AidxEditOutcome> DiscardRevisionAsync<TContent>(
        TContent revision, string expectedVersion, AidxContentType contentType,
        Guid actingUserId, Guid? actingMembershipId, CancellationToken cancellationToken)
        where TContent : class, IAidxWorkflowContent
    {
        if (revision.RevisionOfId is null || revision.MergedAt is not null || revision.DiscardedAt is not null)
        {
            return AidxEditOutcome.InvalidTransition;
        }

        if (!VersionMatches(revision, expectedVersion))
        {
            return AidxEditOutcome.VersionConflict;
        }

        revision.DiscardedAt = DateTimeOffset.UtcNow;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return AidxEditOutcome.VersionConflict;
        }

        db.AidxEditorialAuditEntries.Add(new AidxEditorialAuditEntry
        {
            ContentType = contentType,
            SubjectId = revision.Id,
            Action = AidxEditorialAction.Discarded,
            PerformedByUserId = actingUserId,
            PerformedByMembershipId = actingMembershipId,
        });
        await db.SaveChangesAsync(cancellationToken);

        return AidxEditOutcome.Success;
    }

    public async Task ClaimOwnershipAsync<TContent>(
        TContent content, Guid claimingMembershipId, AidxContentType contentType,
        Guid actingUserId, CancellationToken cancellationToken)
        where TContent : class, IAidxWorkflowContent
    {
        content.CreatedByMembershipId = claimingMembershipId;
        await db.SaveChangesAsync(cancellationToken);

        db.AidxEditorialAuditEntries.Add(new AidxEditorialAuditEntry
        {
            ContentType = contentType,
            SubjectId = content.Id,
            Action = AidxEditorialAction.OwnershipClaimed,
            PerformedByUserId = actingUserId,
            PerformedByMembershipId = claimingMembershipId,
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<AidxEditOutcome> TransitionAsync<TContent>(
        TContent content, string expectedVersion, AidxContentStatus to, AidxContentType contentType,
        AidxEditorialAction action, Guid actingUserId, Guid? actingMembershipId, string? comment,
        CancellationToken cancellationToken)
        where TContent : class, IAidxWorkflowContent
    {
        if (!AidxEditorialWorkflow.CanTransition(content.Status, to))
        {
            return AidxEditOutcome.InvalidTransition;
        }

        if (!VersionMatches(content, expectedVersion))
        {
            return AidxEditOutcome.VersionConflict;
        }

        var latestSnapshot = await db.AidxContentSnapshots
            .Where(s => s.SubjectId == content.Id)
            .OrderByDescending(s => s.SnapshotNumber)
            .FirstOrDefaultAsync(cancellationToken);

        content.Status = to;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return AidxEditOutcome.VersionConflict;
        }

        db.AidxEditorialAuditEntries.Add(new AidxEditorialAuditEntry
        {
            ContentType = contentType,
            SubjectId = content.Id,
            SnapshotId = latestSnapshot?.Id,
            Action = action,
            PerformedByUserId = actingUserId,
            PerformedByMembershipId = actingMembershipId,
            Comment = comment,
        });
        await db.SaveChangesAsync(cancellationToken);

        return AidxEditOutcome.Success;
    }
}
