import { apiClient, ApiError } from "@/lib/auth/client";
import type { Application, ApplicationEligibility, ApplicationSummary } from "./types";

const BASE = "/api/v1/applications";

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
 * browser's save dialog via a temporary, invisible anchor click.
 */
export async function downloadResume(token: string | null, applicationId: string, fileName: string) {
  const response = await fetch(`${BASE}/${applicationId}/resume`, {
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
