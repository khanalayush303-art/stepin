import { apiClient } from "@/lib/auth/client";
import type { PageResult, ResearchArea } from "./types";

/**
 * Admin CMS client for AIDX research areas and projects (Phase 4.4E.1).
 *
 * Research areas have no lifecycle in the backend, so they have no publish or archive operations here.
 * Projects use Draft, Published and Archived, through dedicated lifecycle endpoints. The backend stays
 * authoritative for slugs, relationships and publication. The payloads below carry content fields only.
 */

export type AidxProjectStatus = "Draft" | "Published" | "Archived";

// ---- Research areas ----------------------------------------------------------

export interface ResearchAreaInput {
  name: string;
  slug: string | null;
  description: string | null;
  sortOrder: number;
}

/** Public list. Research areas have no status, so the public list is the full set. */
export function listResearchAreaOptions(search?: string) {
  const term = search?.trim();
  return apiClient.get<ResearchArea[]>(term ? `/api/v1/aidx/research?search=${encodeURIComponent(term)}` : "/api/v1/aidx/research", null);
}

/** Admin view of a research area. Carries the display order so an edit cannot silently reset it. */
export interface AdminResearchArea extends ResearchArea {
  sortOrder: number;
}

export function getAdminResearchArea(token: string | null, id: string) {
  return apiClient.get<AdminResearchArea>(`/api/v1/admin/aidx/research/${id}`, token);
}

export function createAdminResearchArea(token: string | null, input: ResearchAreaInput) {
  return apiClient.post<{ id: string; slug: string | null; status: string | null }>("/api/v1/admin/aidx/research", token, toAreaPayload(input));
}

export function updateAdminResearchArea(token: string | null, id: string, input: ResearchAreaInput) {
  return apiClient.put<{ id: string; slug: string | null; status: string | null }>(`/api/v1/admin/aidx/research/${id}`, token, toAreaPayload(input));
}

export function deleteAdminResearchArea(token: string | null, id: string) {
  return apiClient.delete(`/api/v1/admin/aidx/research/${id}`, token);
}

function toAreaPayload(input: ResearchAreaInput) {
  return {
    name: input.name,
    slug: input.slug,
    description: input.description,
    sortOrder: input.sortOrder,
  };
}

// ---- Projects ----------------------------------------------------------------

export interface AdminProjectSummary {
  id: string;
  title: string;
  slug: string;
  shortDescription: string;
  status: AidxProjectStatus;
  featured: boolean;
  startDate: string | null;
  endDate: string | null;
  publishedAt: string | null;
  updatedAt: string | null;
  researchAreas: string[];
}

export interface AdminProjectResearcher {
  researcherId: string;
  displayName: string;
  role: string | null;
}

export interface AdminProjectDetail {
  id: string;
  title: string;
  slug: string;
  shortDescription: string;
  description: string;
  status: AidxProjectStatus;
  featured: boolean;
  startDate: string | null;
  endDate: string | null;
  externalUrl: string | null;
  publishedAt: string | null;
  researchAreaIds: string[];
  technologies: string[];
  researchers: AdminProjectResearcher[];
}

export interface ProjectFilters {
  status?: AidxProjectStatus | "";
  area?: string;
  featured?: "true" | "false" | "";
  search?: string;
  page?: number;
  pageSize?: number;
}

export interface ProjectInput {
  title: string;
  slug: string | null;
  shortDescription: string;
  description: string;
  startDate: string | null;
  endDate: string | null;
  externalUrl: string | null;
  featured: boolean;
  researchAreaIds: string[];
  technologies: string[];
  /**
   * Carried through unchanged from the loaded project. This form does not edit researcher links yet,
   * and the backend replaces them from the request, so omitting them would drop them.
   */
  researchers: { researcherId: string; role: string | null }[];
}

export function listAdminProjects(token: string | null, filters: ProjectFilters = {}) {
  const params = new URLSearchParams();
  if (filters.status) params.set("status", filters.status);
  if (filters.area) params.set("area", filters.area);
  if (filters.featured) params.set("featured", filters.featured);
  if (filters.search?.trim()) params.set("search", filters.search.trim());
  params.set("page", String(filters.page ?? 1));
  params.set("pageSize", String(filters.pageSize ?? 20));
  return apiClient.get<PageResult<AdminProjectSummary>>(`/api/v1/admin/aidx/projects?${params.toString()}`, token);
}

export function getAdminProject(token: string | null, id: string) {
  return apiClient.get<AdminProjectDetail>(`/api/v1/admin/aidx/projects/${id}`, token);
}

export function createAdminProject(token: string | null, input: ProjectInput) {
  return apiClient.post<{ id: string; slug: string; status: string }>("/api/v1/admin/aidx/projects", token, toProjectPayload(input));
}

export function updateAdminProject(token: string | null, id: string, input: ProjectInput) {
  return apiClient.put<{ id: string; slug: string; status: string }>(`/api/v1/admin/aidx/projects/${id}`, token, toProjectPayload(input));
}

export function publishAdminProject(token: string | null, id: string) {
  return apiClient.post<{ id: string; slug: string; status: string }>(`/api/v1/admin/aidx/projects/${id}/publish`, token);
}

export function archiveAdminProject(token: string | null, id: string) {
  return apiClient.post<{ id: string; slug: string; status: string }>(`/api/v1/admin/aidx/projects/${id}/archive`, token);
}

function toProjectPayload(input: ProjectInput) {
  return {
    title: input.title,
    slug: input.slug,
    shortDescription: input.shortDescription,
    description: input.description,
    startDate: input.startDate,
    endDate: input.endDate,
    externalUrl: input.externalUrl,
    featured: input.featured,
    researchAreaIds: input.researchAreaIds,
    technologies: input.technologies,
    researchers: input.researchers.map((r) => ({ researcherId: r.researcherId, role: r.role })),
  };
}
