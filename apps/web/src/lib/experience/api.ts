import { apiClient } from "@/lib/auth/client";
import type { ExperienceAdminRecord, ExperienceQueueItem, ExperienceRecord, ExperienceRecordInput, ExperienceRecordSummary } from "./types";

const BASE = "/api/v1/experience-passport";
const ADMIN_BASE = "/api/v1/admin/experience-passport";

// ------------------------------------------------------------------ candidate ---

export function listExperienceRecords(token: string | null) {
  return apiClient.get<ExperienceRecordSummary[]>(BASE, token);
}

export function createExperienceRecord(token: string | null, input: ExperienceRecordInput) {
  return apiClient.post<ExperienceRecord>(BASE, token, input);
}

export function getExperienceRecord(token: string | null, id: string) {
  return apiClient.get<ExperienceRecord>(`${BASE}/${id}`, token);
}

export function updateExperienceRecord(token: string | null, id: string, input: ExperienceRecordInput) {
  return apiClient.put<ExperienceRecord>(`${BASE}/${id}`, token, input);
}

export function submitExperienceRecord(token: string | null, id: string) {
  return apiClient.post<ExperienceRecord>(`${BASE}/${id}/submit`, token);
}

export function resubmitExperienceRecord(token: string | null, id: string) {
  return apiClient.post<ExperienceRecord>(`${BASE}/${id}/resubmit`, token);
}

export function withdrawExperienceRecord(token: string | null, id: string, note: string | null) {
  return apiClient.post<ExperienceRecord>(`${BASE}/${id}/withdraw`, token, { note });
}

export function deleteExperienceRecord(token: string | null, id: string) {
  return apiClient.delete(`${BASE}/${id}`, token);
}

// ----------------------------------------------------------------------- admin ---

export function listExperienceQueue(token: string | null, status?: string) {
  const query = status ? `?status=${encodeURIComponent(status)}` : "";
  return apiClient.get<ExperienceQueueItem[]>(`${ADMIN_BASE}/queue${query}`, token);
}

export function getExperienceRecordAdmin(token: string | null, id: string) {
  return apiClient.get<ExperienceAdminRecord>(`${ADMIN_BASE}/${id}`, token);
}

export function beginExperienceReview(token: string | null, id: string) {
  return apiClient.post<ExperienceAdminRecord>(`${ADMIN_BASE}/${id}/begin-review`, token);
}

export function approveExperienceRecord(token: string | null, id: string, note: string | null) {
  return apiClient.post<ExperienceAdminRecord>(`${ADMIN_BASE}/${id}/approve`, token, { note });
}

export function rejectExperienceRecord(token: string | null, id: string, note: string | null) {
  return apiClient.post<ExperienceAdminRecord>(`${ADMIN_BASE}/${id}/reject`, token, { note });
}
