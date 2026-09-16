import { clerkMiddleware } from "@clerk/nextjs/server";

// `createRouteMatcher` + middleware-based route protection is deprecated by
// Clerk in favor of resource-based checks per page/layout (path matching can
// diverge from how Next.js actually routes requests). The actual protection
// for /dashboard, /recruiter, /admin and /account-setup lives in each of
// those route groups' layout.tsx via `auth.protect()`. This middleware only
// makes Clerk's request-scoped auth state available to that code.
export const proxy = clerkMiddleware();

export const config = {
  matcher: ["/((?!_next|.*\\..*).*)", "/(api|trpc)(.*)"],
};
