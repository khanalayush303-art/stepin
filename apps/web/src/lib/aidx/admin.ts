import { apiClient, ApiError } from "@/lib/auth/client";
import type { JobInput } from "@/lib/jobs/types";
import type { PageResult, ProjectSummary } from "./types";

/**
 * Typed client for the AIDX Research Opportunity admin API (Phase 4.4C).
 *
 * The backend is authoritative for ownership, category, lifecycle and publication rules. This module
 * only sends content fields. It never sends owner, company, category or status, so none of those can
 * be chosen from the browser.
 */

export type AidxOpportunityStatus = "Draft" | "Published" | "Unpublished";

export interface AidxOpportunityAdmin {
  id: string;
  title: string;
  description: string;
  employmentType: JobInput["employmentType"];
  workplaceType: JobInput["workplaceType"];
  location: string;
  compensation: string | null;
  skills: string[];
  status: AidxOpportunityStatus;
  aidxProjectId: string | null;
  aidxProjectTitle: string | null;
  aidxProjectSlug: string | null;
  createdAt: string;
  updatedAt: string | null;
  publishedAt: string | null;
}

/** Content fields only. The project is optional. */
export type AidxOpportunityInput = JobInput & { aidxProjectId: string | null };

export interface OpportunityFilters {
  status?: AidxOpportunityStatus | "";
  project?: string;
  search?: string;
  page?: number;
  pageSize?: number;
}

const BASE = "/api/v1/admin/aidx/opportunities";

function withQuery(path: string, params: Record<string, string | number | undefined | null>): string {
  const search = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value === undefined || value === null || value === "") continue;
    search.set(key, String(value));
  }
  const text = search.toString();
  return text ? `${path}?${text}` : path;
}

/** Builds the exact body the backend expects. Ownership, category and status are never included. */
function toPayload(input: AidxOpportunityInput) {
  return {
    title: input.title,
    description: input.description,
    employmentType: input.employmentType,
    workplaceType: input.workplaceType,
    location: input.location,
    compensation: input.compensation,
    skills: input.skills,
    aidxProjectId: input.aidxProjectId,
  };
}

export function listAdminOpportunities(token: string | null, filters: OpportunityFilters = {}) {
  return apiClient.get<PageResult<AidxOpportunityAdmin>>(
    withQuery(BASE, {
      status: filters.status || undefined,
      project: filters.project || undefined,
      search: filters.search?.trim() || undefined,
      page: filters.page,
      pageSize: filters.pageSize ?? 20,
    }),
    token,
  );
}

export function getAdminOpportunity(token: string | null, id: string) {
  return apiClient.get<AidxOpportunityAdmin>(`${BASE}/${id}`, token);
}

export function createAdminOpportunity(token: string | null, input: AidxOpportunityInput) {
  return apiClient.post<AidxOpportunityAdmin>(BASE, token, toPayload(input));
}

export function updateAdminOpportunity(token: string | null, id: string, input: AidxOpportunityInput) {
  return apiClient.put<AidxOpportunityAdmin>(`${BASE}/${id}`, token, toPayload(input));
}

export function publishAdminOpportunity(token: string | null, id: string) {
  return apiClient.post<AidxOpportunityAdmin>(`${BASE}/${id}/publish`, token);
}

export function unpublishAdminOpportunity(token: string | null, id: string) {
  return apiClient.post<AidxOpportunityAdmin>(`${BASE}/${id}/unpublish`, token);
}

export function deleteAdminOpportunity(token: string | null, id: string) {
  return apiClient.delete(`${BASE}/${id}`, token);
}

/**
 * Projects offered in the selector. Uses the public published-projects list: the publish rule only
 * accepts published projects, and this avoids a second project source. A draft project that is already
 * linked still shows its title, from the opportunity's own response.
 */
export function listPublishedProjectOptions() {
  return apiClient.get<PageResult<ProjectSummary>>("/api/v1/aidx/projects?pageSize=50", null);
}

export type AdminOpportunityError =
  | { kind: "unauthenticated"; message: string }
  | { kind: "forbidden"; message: string }
  | { kind: "not-found"; message: string }
  | { kind: "validation"; message: string; fields: Record<string, string[]> }
  | { kind: "unavailable"; message: string }
  | { kind: "server"; message: string };

const MESSAGES = {
  unauthenticated: "Your session has expired. Sign in again to continue.",
  forbidden: "You do not have permission to manage AIDX Lab research opportunities.",
  notFound: "This research opportunity could not be found.",
  unavailable: "The AIDX system owner is unavailable. Ask an administrator to check its setup.",
  server: "Something went wrong on our side. Try again in a moment.",
  network: "We couldn't reach the server. Check your connection and try again.",
};

/**
 * Maps a failed request to a message the UI can show. Raw server bodies and status codes never reach the
 * page, and field-level validation messages stay attached to their fields.
 */
export function describeAdminError(error: unknown): AdminOpportunityError {
  if (!(error instanceof ApiError)) {
    return { kind: "server", message: MESSAGES.network };
  }

  switch (error.status) {
    case 401:
      return { kind: "unauthenticated", message: MESSAGES.unauthenticated };
    case 403:
      return { kind: "forbidden", message: MESSAGES.forbidden };
    case 404:
      return { kind: "not-found", message: MESSAGES.notFound };
    case 400:
    case 422: {
      const fields = error.problem.errors ?? {};
      const first = Object.values(fields).flat()[0];
      return {
        kind: "validation",
        message: first ?? "Check the highlighted fields and try again.",
        fields,
      };
    }
    case 409:
      return { kind: "validation", message: error.problem.detail ?? error.problem.title ?? "This change conflicts with the current state.", fields: {} };
    case 503:
      return { kind: "unavailable", message: MESSAGES.unavailable };
    default:
      return { kind: "server", message: MESSAGES.server };
  }
}
