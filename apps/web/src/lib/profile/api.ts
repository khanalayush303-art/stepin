import { apiClient } from "@/lib/auth/client";
import type {
  CandidateProfile,
  RecruiterProfile,
  UpdateCandidateProfileInput,
  UpdateRecruiterProfileInput,
} from "./types";

const CANDIDATE_BASE = "/api/v1/profile/candidate";
const RECRUITER_BASE = "/api/v1/profile/recruiter";

export function getCandidateProfile(token: string | null) {
  return apiClient.get<CandidateProfile>(CANDIDATE_BASE, token);
}

export function updateCandidateProfile(token: string | null, input: UpdateCandidateProfileInput) {
  return apiClient.put<CandidateProfile>(CANDIDATE_BASE, token, input);
}

export function getRecruiterProfile(token: string | null) {
  return apiClient.get<RecruiterProfile>(RECRUITER_BASE, token);
}

export function updateRecruiterProfile(token: string | null, input: UpdateRecruiterProfileInput) {
  return apiClient.put<RecruiterProfile>(RECRUITER_BASE, token, input);
}
