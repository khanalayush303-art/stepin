/**
 * Client-safe (authenticated) AIDX calls, kept separate from lib/aidx/api.ts, which is
 * server-only and anonymous by design. This one needs the signed-in candidate's own Bearer
 * token, so it uses the same apiClient every other authenticated candidate call uses.
 */
import { apiClient } from "@/lib/auth/client";
import type { ResearchMatch } from "./types";

export function listResearchMatches(token: string | null) {
  return apiClient.get<ResearchMatch[]>("/api/v1/aidx/research-matches", token);
}
