import { apiClient } from "@/lib/auth/client";
import type { AidxWriteResult } from "./cms";
import type { PageResult } from "./types";
import type { AidxWorkflowStatus } from "./workflow-status";

/**
 * Admin CMS client for AIDX events (Phase 4.4E.5).
 *
 * An event has a Draft, Published and Archived lifecycle, and a public URL built from its slug. The backend
 * keeps a stored slug unless one is explicitly sent. Dates travel as ISO strings with an offset. Status and
 * timestamps are changed only through the lifecycle endpoints, never through the body.
 */

export type AidxEventStatus = AidxWorkflowStatus;

export interface AdminEventSummary {
  id: string;
  slug: string;
  title: string;
  status: AidxEventStatus;
  startsAt: string;
  endsAt: string | null;
  location: string | null;
  speakerName: string | null;
}

export interface AdminEventDetail {
  id: string;
  slug: string;
  title: string;
  description: string;
  status: AidxEventStatus;
  startsAt: string;
  endsAt: string | null;
  location: string | null;
  registrationUrl: string | null;
  speakerName: string | null;
  createdAt: string;
  updatedAt: string | null;
  version: string | null;
  hasImage: boolean;
  imageAltText: string | null;
}

export interface EventInput {
  title: string;
  slug: string | null;
  description: string;
  startsAt: string;
  endsAt: string | null;
  location: string | null;
  registrationUrl: string | null;
  speakerName: string | null;
  expectedVersion: string | null;
}

export interface EventFilters {
  q?: string;
  status?: AidxEventStatus | "";
  upcoming?: boolean;
  page?: number;
  pageSize?: number;
}

const BASE = "/api/v1/admin/aidx/events";

export function listAdminEvents(token: string | null, filters: EventFilters = {}) {
  const params = new URLSearchParams();
  if (filters.q?.trim()) params.set("q", filters.q.trim());
  if (filters.status) params.set("status", filters.status);
  if (filters.upcoming) params.set("upcoming", "true");
  params.set("page", String(filters.page ?? 1));
  params.set("pageSize", String(filters.pageSize ?? 20));
  return apiClient.get<PageResult<AdminEventSummary>>(`${BASE}?${params.toString()}`, token);
}

export function getAdminEvent(token: string | null, id: string) {
  return apiClient.get<AdminEventDetail>(`${BASE}/${id}`, token);
}

export function createAdminEvent(token: string | null, input: EventInput) {
  return apiClient.post<AidxWriteResult>(BASE, token, toPayload(input));
}

export function updateAdminEvent(token: string | null, id: string, input: EventInput) {
  return apiClient.put<AidxWriteResult>(`${BASE}/${id}`, token, toPayload(input));
}

export function publishAdminEvent(token: string | null, id: string, expectedVersion: string | null) {
  return apiClient.post<AidxWriteResult>(`${BASE}/${id}/publish`, token, { expectedVersion });
}

export function archiveAdminEvent(token: string | null, id: string, expectedVersion: string | null) {
  return apiClient.post<AidxWriteResult>(`${BASE}/${id}/archive`, token, { expectedVersion });
}

export function deleteAdminEvent(token: string | null, id: string) {
  return apiClient.delete(`${BASE}/${id}`, token);
}

/**
 * Editing an already-published event opens a pending revision, same as any other field edit — the
 * uploaded image does not replace the live one until that revision is approved.
 */
export function uploadAdminEventImage(token: string | null, id: string, file: File, altText: string) {
  const form = new FormData();
  form.set("file", file);
  form.set("altText", altText);
  return apiClient.postForm<{ storageKey: string }>(`/api/v1/aidx/authoring/events/${id}/image`, token, form);
}

export function eventImageUrl(id: string, cacheBust?: number) {
  const suffix = cacheBust ? `?v=${cacheBust}` : "";
  return `/api/v1/aidx/media/events/${id}/image${suffix}`;
}

/** Content and schedule fields only. Status, timestamps and storage keys are never sent. */
function toPayload(input: EventInput) {
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

/**
 * Converts a stored ISO instant to the value a datetime-local input shows, in the browser's local time.
 * The reverse conversion is toIsoFromLocal.
 */
export function toLocalInput(iso: string | null): string {
  if (!iso) return "";
  const date = new Date(iso);
  const pad = (value: number) => String(value).padStart(2, "0");
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

/** Converts a datetime-local value to an ISO instant with an offset. Returns null for an empty value. */
export function toIsoFromLocal(local: string): string | null {
  if (!local) return null;
  const date = new Date(local);
  return Number.isNaN(date.getTime()) ? null : date.toISOString();
}
