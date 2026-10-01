import { apiClient } from "@/lib/auth/client";
import type { SavedJob } from "./types";

const JOBS_BASE = "/api/v1/jobs";
const SAVED_JOBS_BASE = "/api/v1/saved-jobs";

export function listSavedJobs(token: string | null) {
  return apiClient.get<SavedJob[]>(SAVED_JOBS_BASE, token);
}

export function saveJob(token: string | null, jobId: string) {
  return apiClient.post<SavedJob>(`${JOBS_BASE}/${jobId}/saved`, token);
}

export function unsaveJob(token: string | null, jobId: string) {
  return apiClient.delete(`${JOBS_BASE}/${jobId}/saved`, token);
}
