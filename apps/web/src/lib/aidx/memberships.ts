import { apiClient } from "@/lib/auth/client";

export interface AidxMembership {
  id: string;
  userId: string;
  userEmail: string;
  role: "Contributor" | "Editor" | "LabManager";
  status: "Active" | "Revoked";
  scopedResearchAreaId: string | null;
  createdAt: string;
  revokedAt: string | null;
}

export interface AidxUserSearchResult {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
}

const BASE = "/api/v1/admin/aidx/memberships";

export function listMemberships(token: string | null) {
  return apiClient.get<AidxMembership[]>(BASE, token);
}

export function searchUsers(token: string | null, search: string) {
  return apiClient.get<AidxUserSearchResult[]>(`/api/v1/admin/aidx/users?search=${encodeURIComponent(search)}`, token);
}

export function grantMembership(token: string | null, userId: string, role: string, scopedResearchAreaId: string | null) {
  return apiClient.post<AidxMembership>(BASE, token, { userId, role, scopedResearchAreaId });
}

export function updateMembership(token: string | null, id: string, role: string, scopedResearchAreaId: string | null) {
  return apiClient.put<void>(`${BASE}/${id}`, token, { role, scopedResearchAreaId });
}

export function revokeMembership(token: string | null, id: string) {
  return apiClient.post<void>(`${BASE}/${id}/revoke`, token);
}
