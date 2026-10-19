import { apiClient } from "@/lib/auth/client";

/**
 * The membership-gated editorial workflow actions (StepIn.Api.Endpoints.AidxWorkflowEndpoints) —
 * shared by the workspace (Contributor) and manage (reviewer) pages and by every authoring edit
 * page's draft preview / submit / resubmit / revision-history UI.
 */

export type AidxWorkflowContentType = "project" | "news" | "event" | "publication";

export interface AidxRevisionSummary {
  id: string;
  snapshotNumber: number;
  submittedByMembershipId: string | null;
  createdAt: string;
  contentJson: string;
}

export interface AidxAuditEntry {
  id: string;
  action: string;
  snapshotId: string | null;
  comment: string | null;
  createdAt: string;
  performedByName: string;
}

function base(contentType: AidxWorkflowContentType, id: string) {
  return `/api/v1/aidx/content/${contentType}/${id}`;
}

/** The content's current optimistic-concurrency version — fetch this immediately before any
 * workflow transition, since the form's own `expectedVersion` can go stale while the page sits
 * open. Read-only and available to the owner or anyone who can review the content (not just the
 * owner) — unlike `/begin-edit`, which is a Contributor-only action that can open a new revision
 * as a side effect and is the wrong endpoint for a reviewer or for this read-only purpose. */
export async function currentVersion(token: string | null, contentType: AidxWorkflowContentType, id: string) {
  const result = await apiClient.get<{ version: string }>(`${base(contentType, id)}/version`, token);
  return result.version;
}

export function submitForReview(token: string | null, contentType: AidxWorkflowContentType, id: string, expectedVersion: string) {
  return apiClient.post(`${base(contentType, id)}/submit`, token, { expectedVersion });
}

export function withdraw(token: string | null, contentType: AidxWorkflowContentType, id: string, expectedVersion: string) {
  return apiClient.post(`${base(contentType, id)}/withdraw`, token, { expectedVersion });
}

export function startReview(token: string | null, contentType: AidxWorkflowContentType, id: string, expectedVersion: string) {
  return apiClient.post(`${base(contentType, id)}/start-review`, token, { expectedVersion });
}

export function approve(token: string | null, contentType: AidxWorkflowContentType, id: string, expectedVersion: string) {
  return apiClient.post(`${base(contentType, id)}/approve`, token, { expectedVersion });
}

export function requestChanges(
  token: string | null,
  contentType: AidxWorkflowContentType,
  id: string,
  expectedVersion: string,
  comment: string
) {
  return apiClient.post(`${base(contentType, id)}/request-changes`, token, { expectedVersion, comment });
}

export function archiveContent(token: string | null, contentType: AidxWorkflowContentType, id: string, expectedVersion: string) {
  return apiClient.post(`${base(contentType, id)}/archive`, token, { expectedVersion });
}

export function claimOwnership(token: string | null, contentType: AidxWorkflowContentType, id: string) {
  return apiClient.post(`${base(contentType, id)}/claim-ownership`, token);
}

export function listRevisions(token: string | null, contentType: AidxWorkflowContentType, id: string) {
  return apiClient.get<AidxRevisionSummary[]>(`${base(contentType, id)}/revisions`, token);
}

export function listAuditEntries(token: string | null, contentType: AidxWorkflowContentType, id: string) {
  return apiClient.get<AidxAuditEntry[]>(`${base(contentType, id)}/audit`, token);
}

const PLURAL: Record<string, string> = { project: "projects", news: "news", event: "events", publication: "publications" };

export function workspaceEditPath(contentType: AidxWorkflowContentType | string, id: string) {
  const key = contentType.toString().toLowerCase();
  return `/aidx/workspace/${PLURAL[key] ?? `${key}s`}/${id}/edit`;
}
