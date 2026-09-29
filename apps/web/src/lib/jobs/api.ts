import { apiClient } from "@/lib/auth/client";
import type { Job, JobInput, JobSummary } from "./types";

const BASE = "/api/v1/recruiter/jobs";

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
