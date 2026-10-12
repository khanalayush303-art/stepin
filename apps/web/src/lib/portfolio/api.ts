import { apiClient } from "@/lib/auth/client";
import type { PortfolioProject, PortfolioProjectInput } from "./types";

const BASE = "/api/v1/portfolio";

export function listPortfolioProjects(token: string | null) {
  return apiClient.get<PortfolioProject[]>(BASE, token);
}

export function createPortfolioProject(token: string | null, input: PortfolioProjectInput) {
  return apiClient.post<PortfolioProject>(BASE, token, input);
}

export function getPortfolioProject(token: string | null, id: string) {
  return apiClient.get<PortfolioProject>(`${BASE}/${id}`, token);
}

export function updatePortfolioProject(token: string | null, id: string, input: PortfolioProjectInput) {
  return apiClient.put<PortfolioProject>(`${BASE}/${id}`, token, input);
}

export function deletePortfolioProject(token: string | null, id: string) {
  return apiClient.delete(`${BASE}/${id}`, token);
}
