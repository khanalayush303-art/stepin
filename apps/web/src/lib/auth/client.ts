/**
 * Thin fetch wrapper for the two endpoints ASP.NET Core still owns. Always
 * same-origin (see next.config.ts rewrites) and always carries the caller's
 * Clerk session token as a Bearer header — there is no cookie or client-held
 * token beyond what Clerk's own SDK manages.
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

async function request<T>(path: string, token: string | null, init?: RequestInit): Promise<T> {
  const response = await fetch(path, {
    ...init,
    headers: {
      "Content-Type": "application/json",
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
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
  post: <T>(path: string, token: string | null, data?: unknown): Promise<T> =>
    request<T>(path, token, {
      method: "POST",
      body: data === undefined ? undefined : JSON.stringify(data),
    }),
};

export function formError(error: unknown): string {
  if (error instanceof ApiError) {
    return error.problem.detail ?? error.problem.title ?? "Something went wrong. Try again.";
  }
  return "We couldn't reach the server. Check your connection and try again.";
}
