import { apiClient } from "@/lib/auth/client";
import type { AidxWriteResult } from "./cms";
import type { PageResult, PublicationType } from "./types";

/**
 * Admin CMS client for AIDX publications.
 *
 * A publication has no slug and no detail page on the public site. It goes through the same
 * Draft/Submitted/UnderReview/ChangesRequested/Published/Archived workflow as every other AIDX
 * content type, through dedicated publish/archive endpoints — there is no "published" field on
 * the write payload (removed; the backend never accepted it as a direct client-settable value,
 * closing the gap where Status and the legacy Published bool could diverge). Authors, research
 * areas and projects are sent as complete sets: the backend replaces the stored set with the one
 * sent, in order, so the form always sends the full current state.
 */

export type AidxPublicationStatus = "Draft" | "Submitted" | "UnderReview" | "ChangesRequested" | "Published" | "Archived";

export interface AdminPublicationSummary {
  id: string;
  title: string;
  publicationType: PublicationType;
  year: number;
  venue: string | null;
  doi: string | null;
  published: boolean;
  authors: string[];
  status: AidxPublicationStatus;
}

export interface AdminPublicationAuthor {
  position: number;
  researcherId: string | null;
  externalAuthorName: string | null;
  displayName: string;
  /** Null for external authors. False when the linked researcher is currently hidden. */
  researcherPublished: boolean | null;
}

export interface AdminPublicationDetail {
  id: string;
  title: string;
  abstract: string | null;
  publicationType: PublicationType;
  venue: string | null;
  year: number;
  doi: string | null;
  externalUrl: string | null;
  /** Kept for the legacy dual-write period — see the Admin CMS correction's migration notes.
   * The form never sends this; it is set only by publish/archive, in lockstep with status. */
  published: boolean;
  status: AidxPublicationStatus;
  version: string | null;
  authors: AdminPublicationAuthor[];
  researchAreaIds: string[];
  projects: { id: string; title: string; slug: string; status: "Draft" | "Published" | "Archived" }[];
  hasPdf: boolean;
}

export interface PublicationAuthorInput {
  researcherId: string | null;
  externalAuthorName: string | null;
}

export interface PublicationInput {
  title: string;
  abstract: string | null;
  publicationType: PublicationType;
  venue: string | null;
  year: number;
  doi: string | null;
  externalUrl: string | null;
  authors: PublicationAuthorInput[];
  researchAreaIds: string[];
  projectIds: string[];
  expectedVersion: string | null;
}

export interface PublicationFilters {
  q?: string;
  type?: PublicationType | "";
  year?: string;
  published?: "true" | "false" | "";
  page?: number;
  pageSize?: number;
}

const BASE = "/api/v1/admin/aidx/publications";

export function listAdminPublications(token: string | null, filters: PublicationFilters = {}) {
  const params = new URLSearchParams();
  if (filters.q?.trim()) params.set("q", filters.q.trim());
  if (filters.type) params.set("type", filters.type);
  if (filters.year) params.set("year", filters.year);
  if (filters.published) params.set("published", filters.published);
  params.set("page", String(filters.page ?? 1));
  params.set("pageSize", String(filters.pageSize ?? 20));
  return apiClient.get<PageResult<AdminPublicationSummary>>(`${BASE}?${params.toString()}`, token);
}

export function getAdminPublication(token: string | null, id: string) {
  return apiClient.get<AdminPublicationDetail>(`${BASE}/${id}`, token);
}

export function createAdminPublication(token: string | null, input: PublicationInput) {
  return apiClient.post<AidxWriteResult>(BASE, token, toPayload(input));
}

export function updateAdminPublication(token: string | null, id: string, input: PublicationInput) {
  return apiClient.put<AidxWriteResult>(`${BASE}/${id}`, token, toPayload(input));
}

export function publishAdminPublication(token: string | null, id: string, expectedVersion: string | null) {
  return apiClient.post<AidxWriteResult>(`${BASE}/${id}/publish`, token, { expectedVersion });
}

export function archiveAdminPublication(token: string | null, id: string, expectedVersion: string | null) {
  return apiClient.post<AidxWriteResult>(`${BASE}/${id}/archive`, token, { expectedVersion });
}

export function deleteAdminPublication(token: string | null, id: string) {
  return apiClient.delete(`${BASE}/${id}`, token);
}

/**
 * Editing an already-published publication opens a pending revision, same as any other field
 * edit — the uploaded PDF does not replace the live one until that revision is approved.
 */
export function uploadAdminPublicationPdf(token: string | null, id: string, file: File) {
  const form = new FormData();
  form.set("file", file);
  return apiClient.postForm<{ storageKey: string }>(`/api/v1/aidx/authoring/publications/${id}/pdf`, token, form);
}

export function publicationPdfUrl(id: string) {
  return `/api/v1/aidx/media/publications/${id}/pdf`;
}

/** Content and relationship fields only. There is deliberately no "published" field — see this
 * module's own doc comment. Storage keys and internal fields are never sent either. */
function toPayload(input: PublicationInput) {
  return {
    title: input.title,
    abstract: input.abstract,
    publicationType: input.publicationType,
    venue: input.venue,
    year: input.year,
    doi: input.doi,
    externalUrl: input.externalUrl,
    authors: input.authors.map((a) => ({ researcherId: a.researcherId, externalAuthorName: a.externalAuthorName })),
    researchAreaIds: input.researchAreaIds,
    projectIds: input.projectIds,
    expectedVersion: input.expectedVersion,
  };
}
