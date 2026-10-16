import { apiClient } from "@/lib/auth/client";
import type { AidxWriteResult, AdminProjectDetail, ProjectInput } from "./cms";
import type { AdminNewsDetail, NewsInput } from "./news-cms";
import type { AdminEventDetail, EventInput } from "./events-cms";
import type { AdminPublicationDetail, PublicationInput } from "./publications-cms";

/**
 * Contributor-facing authoring client — the same request/response shapes as the Admin CMS client
 * (StepIn.Api.Endpoints.AidxAdminEndpoints' Build*DetailResponse builders are shared verbatim by
 * both route groups server-side), but hitting /api/v1/aidx/authoring/* instead of
 * /api/v1/admin/aidx/*. Ownership is always resolved server-side from the caller's own membership;
 * a Contributor only ever sees and edits their own content through these routes.
 */

const BASE = "/api/v1/aidx/authoring";

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
    outcomes: input.outcomes,
    milestones: input.milestones.map((m) => ({
      title: m.title,
      description: m.description,
      targetDate: m.targetDate,
      completedDate: m.completedDate,
    })),
    expectedVersion: input.expectedVersion,
  };
}

export function getAuthoringProject(token: string | null, id: string) {
  return apiClient.get<AdminProjectDetail>(`${BASE}/projects/${id}`, token);
}

export function createAuthoringProject(token: string | null, input: ProjectInput) {
  return apiClient.post<AidxWriteResult>(`${BASE}/projects`, token, toProjectPayload(input));
}

export function updateAuthoringProject(token: string | null, id: string, input: ProjectInput) {
  return apiClient.put<AidxWriteResult>(`${BASE}/projects/${id}`, token, toProjectPayload(input));
}

export function uploadAuthoringProjectImage(token: string | null, id: string, file: File, altText: string) {
  const form = new FormData();
  form.set("file", file);
  form.set("altText", altText);
  return apiClient.postForm<{ storageKey: string }>(`${BASE}/projects/${id}/image`, token, form);
}

function toNewsPayload(input: NewsInput) {
  return {
    title: input.title,
    slug: input.slug,
    summary: input.summary,
    body: input.body,
    authorResearcherId: input.authorResearcherId,
    expectedVersion: input.expectedVersion,
  };
}

export function getAuthoringNews(token: string | null, id: string) {
  return apiClient.get<AdminNewsDetail>(`${BASE}/news/${id}`, token);
}

export function createAuthoringNews(token: string | null, input: NewsInput) {
  return apiClient.post<AidxWriteResult>(`${BASE}/news`, token, toNewsPayload(input));
}

export function updateAuthoringNews(token: string | null, id: string, input: NewsInput) {
  return apiClient.put<AidxWriteResult>(`${BASE}/news/${id}`, token, toNewsPayload(input));
}

export function uploadAuthoringNewsImage(token: string | null, id: string, file: File, altText: string) {
  const form = new FormData();
  form.set("file", file);
  form.set("altText", altText);
  return apiClient.postForm<{ storageKey: string }>(`${BASE}/news/${id}/image`, token, form);
}

function toEventPayload(input: EventInput) {
  return {
    title: input.title,
    slug: input.slug,
    description: input.description,
    startsAt: input.startsAt,
    endsAt: input.endsAt,
    location: input.location,
    registrationUrl: input.registrationUrl,
    speakerName: input.speakerName,
    expectedVersion: input.expectedVersion,
  };
}

export function getAuthoringEvent(token: string | null, id: string) {
  return apiClient.get<AdminEventDetail>(`${BASE}/events/${id}`, token);
}

export function createAuthoringEvent(token: string | null, input: EventInput) {
  return apiClient.post<AidxWriteResult>(`${BASE}/events`, token, toEventPayload(input));
}

export function updateAuthoringEvent(token: string | null, id: string, input: EventInput) {
  return apiClient.put<AidxWriteResult>(`${BASE}/events/${id}`, token, toEventPayload(input));
}

export function uploadAuthoringEventImage(token: string | null, id: string, file: File, altText: string) {
  const form = new FormData();
  form.set("file", file);
  form.set("altText", altText);
  return apiClient.postForm<{ storageKey: string }>(`${BASE}/events/${id}/image`, token, form);
}

function toPublicationPayload(input: PublicationInput) {
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

export function getAuthoringPublication(token: string | null, id: string) {
  return apiClient.get<AdminPublicationDetail>(`${BASE}/publications/${id}`, token);
}

export function createAuthoringPublication(token: string | null, input: PublicationInput) {
  return apiClient.post<AidxWriteResult>(`${BASE}/publications`, token, toPublicationPayload(input));
}

export function updateAuthoringPublication(token: string | null, id: string, input: PublicationInput) {
  return apiClient.put<AidxWriteResult>(`${BASE}/publications/${id}`, token, toPublicationPayload(input));
}

export function uploadAuthoringPublicationPdf(token: string | null, id: string, file: File) {
  const form = new FormData();
  form.set("file", file);
  return apiClient.postForm<{ storageKey: string }>(`${BASE}/publications/${id}/pdf`, token, form);
}
