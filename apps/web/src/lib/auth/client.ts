/**
 * Thin fetch wrapper for the auth API. Always same-origin (see next.config.ts
 * rewrites) and always `credentials: "include"` so the HttpOnly session
 * cookie rides along — there is no token for this client to hold, in memory
 * or otherwise.
 */

export interface ProblemDetails {
  title?: string;
  status?: number;
  detail?: string;
  errors?: Record<string, string[]>;
  [key: string]: unknown;
}

export class ApiError extends Error {
  readonly status: number;
  readonly problem: ProblemDetails;

  constructor(status: number, problem: ProblemDetails) {
    super(problem.title ?? "Something went wrong. Try again.");
    this.status = status;
    this.problem = problem;
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(path, {
    ...init,
    credentials: "include",
    headers: {
      "Content-Type": "application/json",
      // Cheap CSRF defense-in-depth the backend's RequireFetchHeaderFilter
      // checks on state-changing endpoints; harmless elsewhere.
      "X-Requested-With": "fetch",
      ...init?.headers,
    },
  });

  const contentType = response.headers.get("content-type") ?? "";
  const body = contentType.includes("json") ? await response.json() : undefined;

  if (!response.ok) {
    throw new ApiError(response.status, (body as ProblemDetails) ?? {});
  }

  return body as T;
}

export const apiClient = {
  get: <T>(path: string): Promise<T> => request<T>(path, { method: "GET" }),
  post: <T>(path: string, data?: unknown): Promise<T> =>
    request<T>(path, {
      method: "POST",
      body: data === undefined ? undefined : JSON.stringify(data),
    }),
};

/** Pulls the first field-level error for `field`, falling back to the problem's title. */
export function fieldError(error: unknown, field: string): string | undefined {
  if (!(error instanceof ApiError)) return undefined;
  const fieldErrors = error.problem.errors?.[field] ?? error.problem.errors?.[capitalize(field)];
  return fieldErrors?.[0];
}

export function formError(error: unknown): string {
  if (error instanceof ApiError) {
    return error.problem.detail ?? error.problem.title ?? "Something went wrong. Try again.";
  }
  return "We couldn't reach the server. Check your connection and try again.";
}

function capitalize(value: string): string {
  return value.length === 0 ? value : value[0].toUpperCase() + value.slice(1);
}
