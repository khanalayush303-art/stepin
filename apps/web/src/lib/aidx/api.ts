import "server-only";
import { apiOrigin } from "@/lib/auth/server";
import type { PageResult } from "./types";

/** Raised when the API is unreachable or answers with a server error. Pages render an error state. */
export class AidxUnavailableError extends Error {
  constructor(message = "AIDX content is temporarily unavailable.") {
    super(message);
  }
}

/**
 * Public, anonymous GET against the StepIn API. Server-side only, so the browser never
 * sees the API origin. A 404 resolves to null so detail pages can call notFound().
 * Responses are cached for a minute: AIDX content changes rarely and this keeps list pages cheap.
 */
export async function aidxGet<T>(path: string): Promise<T | null> {
  let response: Response;
  try {
    response = await fetch(`${apiOrigin()}/api/v1/aidx${path}`, { next: { revalidate: 60 } });
  } catch {
    throw new AidxUnavailableError();
  }

  if (response.status === 404) {
    return null;
  }
  if (!response.ok) {
    throw new AidxUnavailableError();
  }
  return (await response.json()) as T;
}

/**
 * Public, anonymous GET against a StepIn (non-AIDX) API route, for example a research
 * opportunity's job detail. Same rules as aidxGet: a 404 resolves to null, other failures throw.
 */
export async function stepInPublicGet<T>(path: string): Promise<T | null> {
  let response: Response;
  try {
    response = await fetch(`${apiOrigin()}/api/v1${path}`, { next: { revalidate: 60 } });
  } catch {
    throw new AidxUnavailableError();
  }

  if (response.status === 404) {
    return null;
  }
  if (!response.ok) {
    throw new AidxUnavailableError();
  }
  return (await response.json()) as T;
}

/** Builds a query string from a record, dropping empty values so URLs stay clean. */
export function query(params: Record<string, string | number | boolean | undefined | null>): string {
  const search = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value === undefined || value === null || value === "") continue;
    search.set(key, String(value));
  }
  const text = search.toString();
  return text ? `?${text}` : "";
}

/** Reads a page number from a search param, defaulting to 1 for anything missing or malformed. */
export function parsePage(raw: string | undefined): number {
  const value = Number.parseInt(raw ?? "1", 10);
  return Number.isFinite(value) && value > 0 ? value : 1;
}

/**
 * Non-throwing wrapper for page sections. Each section fails on its own, so one unavailable
 * endpoint leaves the rest of the page usable.
 */
export async function trySection<T>(path: string): Promise<{ data: T | null; failed: boolean }> {
  try {
    return { data: await aidxGet<T>(path), failed: false };
  } catch {
    return { data: null, failed: true };
  }
}

export type { PageResult };
