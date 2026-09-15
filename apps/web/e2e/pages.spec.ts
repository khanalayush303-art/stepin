import { test, expect } from "@playwright/test";

const PAGES = [
  { path: "/", heading: "Find opportunities. Apply with confidence." },
  { path: "/jobs", heading: "Graduate & entry-level jobs" },
  { path: "/internships", heading: "Internships & placements" },
  { path: "/companies", heading: "Employers on StepIn" },
  { path: "/how-it-works", heading: "How StepIn works" },
  { path: "/about", heading: "About StepIn" },
  { path: "/sign-in", heading: "Welcome back" },
  { path: "/register", heading: "Create your account" },
  { path: "/forgot-password", heading: "Reset your password" },
  // A bare /reset-password has no token, so it's an invalid-link state.
  { path: "/reset-password?email=test%40example.com&token=sample-token", heading: "Choose a new password" },
  { path: "/verify-email", heading: "Check your email" },
  // /dashboard, /recruiter and /admin are protected routes (see proxy.ts),
  // covered by e2e/auth.spec.ts instead of this public-heading list.
];

test.describe("every route renders with exactly one h1", () => {
  for (const page of PAGES) {
    test(`${page.path} renders`, async ({ page: browserPage }) => {
      const response = await browserPage.goto(page.path);
      expect(response?.status(), `${page.path} should return 200`).toBe(200);

      const h1 = browserPage.locator("h1");
      await expect(h1).toHaveCount(1);
      await expect(h1).toContainText(page.heading);

      // No horizontal overflow at any viewport.
      const overflow = await browserPage.evaluate(
        () => document.documentElement.scrollWidth - document.documentElement.clientWidth
      );
      expect(overflow, `${page.path} must not scroll horizontally`).toBeLessThanOrEqual(1);
    });
  }
});

test("404 page is served for an unknown route", async ({ page }) => {
  const response = await page.goto("/this-route-does-not-exist");
  expect(response?.status()).toBe(404);
  await expect(page.getByRole("heading", { name: /could not find that page/i })).toBeVisible();
});
