import { apiClient, ApiError } from "@/lib/auth/client";
import type {
  Application,
  ApplicationEligibility,
  ApplicationStatus,
  ApplicationSummary,
  RecruiterApplication,
  RecruiterApplicationFilters,
  RecruiterApplicationSummary,
} from "./types";

const BASE = "/api/v1/applications";
const RECRUITER_BASE = "/api/v1/recruiter";

export function listApplications(token: string | null) {
  return apiClient.get<ApplicationSummary[]>(BASE, token);
}

export function getApplication(token: string | null, id: string) {
  return apiClient.get<Application>(`${BASE}/${id}`, token);
}

export function getApplicationEligibility(token: string | null, jobId: string) {
  return apiClient.get<ApplicationEligibility>(`/api/v1/jobs/${jobId}/applications/mine`, token);
}

export function submitApplication(token: string | null, jobId: string, resume: File, coverLetter: string) {
  const form = new FormData();
  form.set("Resume", resume);
  if (coverLetter.trim()) {
    form.set("CoverLetter", coverLetter.trim());
  }
  return apiClient.postForm<Application>(`/api/v1/jobs/${jobId}/applications`, token, form);
}

/**
 * A plain <a href> can't carry a Bearer Authorization header, and this API
 * has no cookie session to fall back on — so the download goes through an
 * authenticated fetch, converts the response to a Blob, and triggers the
 * browser's save dialog via a temporary, invisible anchor click. Shared by
 * both the candidate's own download and the recruiter's below — same
 * mechanism, different (already ownership-scoped) path.
 */
async function downloadFile(path: string, token: string | null, fileName: string) {
  const response = await fetch(path, {
    headers: token ? { Authorization: `Bearer ${token}` } : {},
  });

  if (!response.ok) {
    const contentType = response.headers.get("content-type") ?? "";
    const body = contentType.includes("json") ? await response.json() : {};
    throw new ApiError(response.status, body);
  }

  const blob = await response.blob();
  const url = URL.createObjectURL(blob);
  try {
    const link = document.createElement("a");
    link.href = url;
    link.download = fileName;
    link.click();
  } finally {
    URL.revokeObjectURL(url);
  }
}

export function downloadResume(token: string | null, applicationId: string, fileName: string) {
  return downloadFile(`${BASE}/${applicationId}/resume`, token, fileName);
}

// ------------------------------------------------------------- recruiter ---
// Ownership (recruiter must own the job) is enforced entirely server-side;
// these calls carry no client-side authorization logic.

export function listRecruiterApplicationsForJob(token: string | null, jobId: string, filters: RecruiterApplicationFilters = {}) {
  const params = new URLSearchParams();
  if (filters.status) params.set("status", filters.status);
  if (filters.search) params.set("search", filters.search);
  if (filters.sort) params.set("sort", filters.sort);

  const query = params.toString();
  return apiClient.get<RecruiterApplicationSummary[]>(`${RECRUITER_BASE}/jobs/${jobId}/applications${query ? `?${query}` : ""}`, token);
}

export function getRecruiterApplication(token: string | null, id: string) {
  return apiClient.get<RecruiterApplication>(`${RECRUITER_BASE}/applications/${id}`, token);
}

export function downloadRecruiterResume(token: string | null, applicationId: string, fileName: string) {
  return downloadFile(`${RECRUITER_BASE}/applications/${applicationId}/resume`, token, fileName);
}

export function updateRecruiterApplicationStatus(token: string | null, id: string, status: ApplicationStatus) {
  return apiClient.put<RecruiterApplication>(`${RECRUITER_BASE}/applications/${id}/status`, token, { status });
}
