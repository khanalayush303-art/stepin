import { apiClient } from "@/lib/auth/client";
import type { CareerGoal, CareerGoalInput, CareerGoalSkillGap, RoadmapItem, RoadmapItemInput, UpdateCareerGoalInput } from "./types";

const BASE = "/api/v1/career-goals";

export function listCareerGoals(token: string | null) {
  return apiClient.get<CareerGoal[]>(BASE, token);
}

export function createCareerGoal(token: string | null, input: CareerGoalInput) {
  return apiClient.post<CareerGoal>(BASE, token, input);
}

export function getCareerGoal(token: string | null, id: string) {
  return apiClient.get<CareerGoal>(`${BASE}/${id}`, token);
}

export function updateCareerGoal(token: string | null, id: string, input: UpdateCareerGoalInput) {
  return apiClient.put<CareerGoal>(`${BASE}/${id}`, token, input);
}

export function completeCareerGoal(token: string | null, id: string) {
  return apiClient.post<CareerGoal>(`${BASE}/${id}/complete`, token);
}

export function archiveCareerGoal(token: string | null, id: string) {
  return apiClient.post<CareerGoal>(`${BASE}/${id}/archive`, token);
}

export function reactivateCareerGoal(token: string | null, id: string) {
  return apiClient.post<CareerGoal>(`${BASE}/${id}/reactivate`, token);
}

export function deleteCareerGoal(token: string | null, id: string) {
  return apiClient.delete(`${BASE}/${id}`, token);
}

/** Throws ApiError with status 404 when the goal has no target job — that's expected, not a bug. */
export function getCareerGoalSkillGap(token: string | null, id: string) {
  return apiClient.get<CareerGoalSkillGap>(`${BASE}/${id}/skill-gap`, token);
}

export function listRoadmapItems(token: string | null, goalId: string) {
  return apiClient.get<RoadmapItem[]>(`${BASE}/${goalId}/roadmap-items`, token);
}

export function createRoadmapItem(token: string | null, goalId: string, input: RoadmapItemInput) {
  return apiClient.post<RoadmapItem>(`${BASE}/${goalId}/roadmap-items`, token, input);
}

export function updateRoadmapItem(token: string | null, goalId: string, itemId: string, input: RoadmapItemInput) {
  return apiClient.put<RoadmapItem>(`${BASE}/${goalId}/roadmap-items/${itemId}`, token, input);
}

export function startRoadmapItem(token: string | null, goalId: string, itemId: string) {
  return apiClient.post<RoadmapItem>(`${BASE}/${goalId}/roadmap-items/${itemId}/start`, token);
}

export function completeRoadmapItem(token: string | null, goalId: string, itemId: string) {
  return apiClient.post<RoadmapItem>(`${BASE}/${goalId}/roadmap-items/${itemId}/complete`, token);
}

export function reopenRoadmapItem(token: string | null, goalId: string, itemId: string) {
  return apiClient.post<RoadmapItem>(`${BASE}/${goalId}/roadmap-items/${itemId}/reopen`, token);
}

export function deleteRoadmapItem(token: string | null, goalId: string, itemId: string) {
  return apiClient.delete(`${BASE}/${goalId}/roadmap-items/${itemId}`, token);
}
