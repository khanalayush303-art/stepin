import { apiClient } from "@/lib/auth/client";
import type { PageResult } from "./types";

/**
 * Admin CMS client for AIDX news (Phase 4.4E.4).
 *
 * News has a Draft, Published and Archived lifecycle, and each item has a public URL built from its slug.
 * The backend keeps a stored slug unless one is explicitly sent, so the form always sends the current slug.
 * Status and publish dates are changed only through the lifecycle endpoints, never through the body.
 */

export type AidxNewsStatus = "Draft" | "Published" | "Archived";

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
}

export interface NewsInput {
  title: string;
  slug: string | null;
  summary: string;
  body: string;
  authorResearcherId: string | null;
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
  return apiClient.post<{ id: string; slug: string; status: AidxNewsStatus }>(BASE, token, toPayload(input));
}

export function updateAdminNews(token: string | null, id: string, input: NewsInput) {
  return apiClient.put<{ id: string; slug: string; status: AidxNewsStatus }>(`${BASE}/${id}`, token, toPayload(input));
}

export function publishAdminNews(token: string | null, id: string) {
  return apiClient.post<{ id: string; slug: string; status: AidxNewsStatus }>(`${BASE}/${id}/publish`, token);
}

export function archiveAdminNews(token: string | null, id: string) {
  return apiClient.post<{ id: string; slug: string; status: AidxNewsStatus }>(`${BASE}/${id}/archive`, token);
}

export function deleteAdminNews(token: string | null, id: string) {
  return apiClient.delete(`${BASE}/${id}`, token);
}

/** Content and author fields only. Status, timestamps and storage keys are never sent. */
function toPayload(input: NewsInput) {
  return {
    title: input.title,
    slug: input.slug,
    summary: input.summary,
    body: input.body,
    authorResearcherId: input.authorResearcherId,
  };
}
