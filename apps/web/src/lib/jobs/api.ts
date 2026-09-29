import { apiClient } from "@/lib/auth/client";
import type { Job, JobFilters, JobInput, JobSummary, PublicJob, PublicJobSummary } from "./types";

const BASE = "/api/v1/recruiter/jobs";
const PUBLIC_BASE = "/api/v1/jobs";

export function listJobs(token: string | null) {
  return apiClient.get<JobSummary[]>(BASE, token);
}

export function getJob(token: string | null, id: string) {
  return apiClient.get<Job>(`${BASE}/${id}`, token);
}

export function createJob(token: string | null, input: JobInput) {
  return apiClient.post<Job>(BASE, token, input);
}

export function updateJob(token: string | null, id: string, input: JobInput) {
  return apiClient.put<Job>(`${BASE}/${id}`, token, input);
}

export function publishJob(token: string | null, id: string) {
  return apiClient.post<Job>(`${BASE}/${id}/publish`, token);
}

export function unpublishJob(token: string | null, id: string) {
  return apiClient.post<Job>(`${BASE}/${id}/unpublish`, token);
}

// -------------------------------------------------------- candidate/public ---
// Genuinely public: no token is sent, and none is required. The dashboard
// route these are called from is what restricts who reaches the UI — the API
// itself needs nothing.

export function listPublicJobs(filters: JobFilters = {}) {
  const params = new URLSearchParams();
  if (filters.search) params.set("search", filters.search);
  if (filters.employmentType) params.set("employmentType", filters.employmentType);
  if (filters.workplaceType) params.set("workplaceType", filters.workplaceType);
  if (filters.location) params.set("location", filters.location);

  const query = params.toString();
  return apiClient.get<PublicJobSummary[]>(`${PUBLIC_BASE}${query ? `?${query}` : ""}`, null);
}

export function getPublicJob(id: string) {
  return apiClient.get<PublicJob>(`${PUBLIC_BASE}/${id}`, null);
}
