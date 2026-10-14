import { test, expect } from "@playwright/test";

// Covers what's frontend-observable without a real Clerk application configured.
// Credentials, sessions, Google, email verification and password reset are all
// Clerk's own hosted flow now; the app-level pieces (user sync, role policies,
// account setup) are covered by the persistence-backed xUnit suite instead
// (apps/api/tests/StepIn.Api.Tests/AuthEndpointsTests.cs).

const PROTECTED_ROUTES = ["/dashboard", "/recruiter", "/admin", "/account-setup"];

for (const path of PROTECTED_ROUTES) {
  test(`${path} redirects an unauthenticated visitor to sign-in`, async ({ page }) => {
    await page.goto(path);
    await expect(page).toHaveURL(new RegExp(`/sign-in(\\?returnTo=${encodeURIComponent(path)})?`));
  });
}

test("sign-in renders without an active session", async ({ page }) => {
  await page.goto("/sign-in");
  await expect(page.getByRole("heading", { level: 1 })).toContainText("Welcome back");
});

test("register renders without an active session", async ({ page }) => {
  await page.goto("/register");
  await expect(page.getByRole("heading", { level: 1 })).toContainText("Create your account");
});

// Nested under (app)/dashboard and (app)/recruiter, so they're covered by the
// same auth.protect() as their parent — but that layout's unauthenticatedUrl
// hardcodes returnTo to the parent route, not the nested path.
test("/dashboard/profile redirects an unauthenticated visitor to sign-in", async ({ page }) => {
  await page.goto("/dashboard/profile");
  await expect(page).toHaveURL(/\/sign-in\?returnTo=%2Fdashboard$/);
});

test("/recruiter/profile redirects an unauthenticated visitor to sign-in", async ({ page }) => {
  await page.goto("/recruiter/profile");
  await expect(page).toHaveURL(/\/sign-in\?returnTo=%2Frecruiter$/);
});

for (const path of [
  "/recruiter/jobs",
  "/recruiter/jobs/new",
  "/recruiter/jobs/123/edit",
  "/recruiter/applications",
  "/recruiter/applications/123",
]) {
  test(`${path} redirects an unauthenticated visitor to sign-in`, async ({ page }) => {
    await page.goto(path);
    await expect(page).toHaveURL(/\/sign-in\?returnTo=%2Frecruiter$/);
  });
}

for (const path of [
  "/dashboard/discover",
  "/dashboard/discover/123",
  "/dashboard/discover/123/apply",
  "/dashboard/applications",
  "/dashboard/applications/123",
  "/dashboard/career-gps",
  "/dashboard/career-gps/123",
  "/dashboard/portfolio",
  "/dashboard/portfolio/123",
  "/dashboard/interview-practice",
  "/dashboard/interview-practice/123",
  "/dashboard/experience-passport",
  "/dashboard/experience-passport/123",
  "/dashboard/prepare",
  "/dashboard/notifications",
]) {
  test(`${path} redirects an unauthenticated visitor to sign-in, not the 404 page`, async ({ page }) => {
    const response = await page.goto(path);
    // A route that doesn't exist would either 404 outright or land on Next's global
    // not-found boundary after any redirect; a real, Applicant-protected route instead
    // redirects (307) straight to sign-in with the right returnTo — proving the route
    // itself resolves, not just that *something* eventually rendered.
    expect(response?.status()).toBeLessThan(400);
    await expect(page).toHaveURL(/\/sign-in\?returnTo=%2Fdashboard$/);
    await expect(page.getByText("We could not find that page")).toHaveCount(0);
  });
}

for (const path of ["/admin/verification", "/admin/verification/123"]) {
  test(`${path} redirects an unauthenticated visitor to sign-in`, async ({ page }) => {
    await page.goto(path);
    await expect(page).toHaveURL(/\/sign-in\?returnTo=%2Fadmin$/);
  });
}

test("old forgot-password/reset-password/verify-email links redirect to sign-in", async ({ page }) => {
  await page.goto("/forgot-password");
  await expect(page).toHaveURL(/\/sign-in$/);

  await page.goto("/reset-password");
  await expect(page).toHaveURL(/\/sign-in$/);

  await page.goto("/verify-email");
  await expect(page).toHaveURL(/\/sign-in$/);
});
