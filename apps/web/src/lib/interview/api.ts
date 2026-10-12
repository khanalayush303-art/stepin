import { apiClient } from "@/lib/auth/client";
import type { CreateInterviewSessionInput, InterviewQuestion, InterviewSession, InterviewSessionSummary, InterviewTemplates } from "./types";

const BASE = "/api/v1/interview-practice";

export function getInterviewTemplates(token: string | null) {
  return apiClient.get<InterviewTemplates>(`${BASE}/templates`, token);
}

export function listInterviewSessions(token: string | null) {
  return apiClient.get<InterviewSessionSummary[]>(BASE, token);
}

export function createInterviewSession(token: string | null, input: CreateInterviewSessionInput) {
  return apiClient.post<InterviewSession>(BASE, token, input);
}

export function getInterviewSession(token: string | null, id: string) {
  return apiClient.get<InterviewSession>(`${BASE}/${id}`, token);
}

export function saveInterviewAnswer(token: string | null, sessionId: string, questionId: string, answerText: string | null) {
  return apiClient.put<InterviewQuestion>(`${BASE}/${sessionId}/questions/${questionId}/answer`, token, { answerText });
}

export function completeInterviewSession(token: string | null, id: string) {
  return apiClient.post<InterviewSession>(`${BASE}/${id}/complete`, token);
}

export function deleteInterviewSession(token: string | null, id: string) {
  return apiClient.delete(`${BASE}/${id}`, token);
}
