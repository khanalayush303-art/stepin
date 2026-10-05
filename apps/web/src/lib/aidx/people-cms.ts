import { apiClient } from "@/lib/auth/client";
import type { PageResult, ResearcherCategory } from "./types";

/**
 * Admin CMS client for AIDX researchers / people (Phase 4.4E.2).
 *
 * A researcher is a content record, not an application user. Visibility is one flag, Published:
 * unpublished people are hidden from every public page. Linked projects and publications are read-only
 * here. Publication authorship is never edited through this client.
 */

export interface AdminPersonSummary {
  id: string;
  slug: string;
  displayName: string;
  category: ResearcherCategory;
  position: string | null;
  published: boolean;
}

export interface AdminPersonProject {
  id: string;
  title: string;
  slug: string;
  status: "Draft" | "Published" | "Archived";
}

export interface AdminPersonPublication {
  id: string;
  title: string;
  year: number;
}

export interface AdminPersonDetail {
  id: string;
  slug: string;
  displayName: string;
  category: ResearcherCategory;
  position: string | null;
  biography: string | null;
  orcidUrl: string | null;
  googleScholarUrl: string | null;
  linkedInUrl: string | null;
  websiteUrl: string | null;
  published: boolean;
  projects: AdminPersonProject[];
  publications: AdminPersonPublication[];
}

export interface PersonInput {
  displayName: string;
  slug: string | null;
  category: ResearcherCategory;
  position: string | null;
  biography: string | null;
  orcidUrl: string | null;
  googleScholarUrl: string | null;
  linkedInUrl: string | null;
  websiteUrl: string | null;
  published: boolean;
}

export interface PeopleFilters {
  search?: string;
  category?: ResearcherCategory | "";
  published?: "true" | "false" | "";
  page?: number;
  pageSize?: number;
}

const BASE = "/api/v1/admin/aidx/people";

export function listAdminPeople(token: string | null, filters: PeopleFilters = {}) {
  const params = new URLSearchParams();
  if (filters.search?.trim()) params.set("search", filters.search.trim());
  if (filters.category) params.set("category", filters.category);
  if (filters.published) params.set("published", filters.published);
  params.set("page", String(filters.page ?? 1));
  params.set("pageSize", String(filters.pageSize ?? 20));
  return apiClient.get<PageResult<AdminPersonSummary>>(`${BASE}?${params.toString()}`, token);
}

export function getAdminPerson(token: string | null, id: string) {
  return apiClient.get<AdminPersonDetail>(`${BASE}/${id}`, token);
}

export function createAdminPerson(token: string | null, input: PersonInput) {
  return apiClient.post<{ id: string; slug: string }>(BASE, token, toPayload(input));
}

export function updateAdminPerson(token: string | null, id: string, input: PersonInput) {
  return apiClient.put<{ id: string; slug: string }>(`${BASE}/${id}`, token, toPayload(input));
}

export function deleteAdminPerson(token: string | null, id: string) {
  return apiClient.delete(`${BASE}/${id}`, token);
}

/** Content fields only. No account, owner or storage fields can be sent from here. */
function toPayload(input: PersonInput) {
  return {
    displayName: input.displayName,
    slug: input.slug,
    category: input.category,
    position: input.position,
    biography: input.biography,
    orcidUrl: input.orcidUrl,
    googleScholarUrl: input.googleScholarUrl,
    linkedInUrl: input.linkedInUrl,
    websiteUrl: input.websiteUrl,
    published: input.published,
  };
}
