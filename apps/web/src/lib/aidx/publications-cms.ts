import { apiClient } from "@/lib/auth/client";
import type { PageResult, PublicationType } from "./types";

/**
 * Admin CMS client for AIDX publications (Phase 4.4E.3).
 *
 * A publication has no slug and no detail page on the public site. Its visibility is one flag, Published.
 * Authors, research areas and projects are sent as complete sets: the backend replaces the stored set with
 * the one sent, in order, so the form always sends the full current state.
 */

export interface AdminPublicationSummary {
  id: string;
  title: string;
  publicationType: PublicationType;
  year: number;
  venue: string | null;
  doi: string | null;
  published: boolean;
  authors: string[];
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
  published: boolean;
  authors: AdminPublicationAuthor[];
  researchAreaIds: string[];
  projects: { id: string; title: string; slug: string; status: "Draft" | "Published" | "Archived" }[];
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
  published: boolean;
  authors: PublicationAuthorInput[];
  researchAreaIds: string[];
  projectIds: string[];
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
  return apiClient.post<{ id: string }>(BASE, token, toPayload(input));
}

export function updateAdminPublication(token: string | null, id: string, input: PublicationInput) {
  return apiClient.put<{ id: string }>(`${BASE}/${id}`, token, toPayload(input));
}

export function deleteAdminPublication(token: string | null, id: string) {
  return apiClient.delete(`${BASE}/${id}`, token);
}

/** Content and relationship fields only. Storage keys and internal fields are never sent. */
function toPayload(input: PublicationInput) {
  return {
    title: input.title,
    abstract: input.abstract,
    publicationType: input.publicationType,
    venue: input.venue,
    year: input.year,
    doi: input.doi,
    externalUrl: input.externalUrl,
    published: input.published,
    authors: input.authors.map((a) => ({ researcherId: a.researcherId, externalAuthorName: a.externalAuthorName })),
    researchAreaIds: input.researchAreaIds,
    projectIds: input.projectIds,
  };
}
