import { apiClient } from "./client";

const base = "/api/v1/auth";

export function completeAccountSetup(token: string | null, role: "Applicant" | "Recruiter") {
  return apiClient.post<{ redirectTo: string }>(`${base}/account-setup`, token, { role });
}
