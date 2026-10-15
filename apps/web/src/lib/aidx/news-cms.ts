import { apiClient } from "@/lib/auth/client";
import type { AidxWriteResult } from "./cms";
import type { PageResult } from "./types";
import type { AidxWorkflowStatus } from "./workflow-status";

/**
 * Admin CMS client for AIDX news (Phase 4.4E.4).
 *
 * News has a Draft, Published and Archived lifecycle, and each item has a public URL built from its slug.
 * The backend keeps a stored slug unless one is explicitly sent, so the form always sends the current slug.
 * Status and publish dates are changed only through the lifecycle endpoints, never through the body.
 */

export type AidxNewsStatus = AidxWorkflowStatus;

export interface AdminNewsSummary {
  id: string;
  slug: string;
  title: string;
  summary: string;
  status: AidxNewsStatus;
  publishedAt: string | null;
  createdAt: string;
  updatedAt: string | null;
}

export interface AdminNewsDetail extends AdminNewsSummary {
  body: string;
  authorResearcherId: string | null;
  authorName: string | null;
  version: string | null;
  hasImage: boolean;
  imageAltText: string | null;
}

export interface NewsInput {
  title: string;
  slug: string | null;
  summary: string;
  body: string;
  authorResearcherId: string | null;
  expectedVersion: string | null;
}

export interface NewsFilters {
  q?: string;
  status?: AidxNewsStatus | "";
  page?: number;
  pageSize?: number;
}

const BASE = "/api/v1/admin/aidx/news";

export function listAdminNews(token: string | null, filters: NewsFilters = {}) {
  const params = new URLSearchParams();
  if (filters.q?.trim()) params.set("q", filters.q.trim());
  if (filters.status) params.set("status", filters.status);
  params.set("page", String(filters.page ?? 1));
  params.set("pageSize", String(filters.pageSize ?? 20));
  return apiClient.get<PageResult<AdminNewsSummary>>(`${BASE}?${params.toString()}`, token);
}

export function getAdminNews(token: string | null, id: string) {
  return apiClient.get<AdminNewsDetail>(`${BASE}/${id}`, token);
}

export function createAdminNews(token: string | null, input: NewsInput) {
  return apiClient.post<AidxWriteResult>(BASE, token, toPayload(input));
}

export function updateAdminNews(token: string | null, id: string, input: NewsInput) {
  return apiClient.put<AidxWriteResult>(`${BASE}/${id}`, token, toPayload(input));
}

export function publishAdminNews(token: string | null, id: string, expectedVersion: string | null) {
  return apiClient.post<AidxWriteResult>(`${BASE}/${id}/publish`, token, { expectedVersion });
}

export function archiveAdminNews(token: string | null, id: string, expectedVersion: string | null) {
  return apiClient.post<AidxWriteResult>(`${BASE}/${id}/archive`, token, { expectedVersion });
}

export function deleteAdminNews(token: string | null, id: string) {
  return apiClient.delete(`${BASE}/${id}`, token);
}

/**
 * Editing an already-published news item opens a pending revision, same as any other field edit —
 * the uploaded image does not replace the live one until that revision is approved.
 */
export function uploadAdminNewsImage(token: string | null, id: string, file: File, altText: string) {
  const form = new FormData();
  form.set("file", file);
  form.set("altText", altText);
  return apiClient.postForm<{ storageKey: string }>(`/api/v1/aidx/authoring/news/${id}/image`, token, form);
}

export function newsImageUrl(id: string, cacheBust?: number) {
  const suffix = cacheBust ? `?v=${cacheBust}` : "";
  return `/api/v1/aidx/media/news/${id}/image${suffix}`;
}

/** Content and author fields only. Status, timestamps and storage keys are never sent. */
function toPayload(input: NewsInput) {
  return {
    title: input.title,
    slug: input.slug,
    summary: input.summary,
    body: input.body,
    authorResearcherId: input.authorResearcherId,
    expectedVersion: input.expectedVersion,
  };
}
