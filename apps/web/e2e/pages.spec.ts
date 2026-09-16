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
  // forgot-password/reset-password/verify-email now redirect to /sign-in
  // (Clerk owns those flows); /dashboard, /recruiter, /admin and
  // /account-setup are protected routes — both covered by e2e/auth.spec.ts
  // instead of this public-heading list.
];

test.describe("every route renders with exactly one h1", () => {
  for (const page of PAGES) {
    test(`${page.path} renders`, async ({ page: browserPage }) => {
      const response = await browserPage.goto(page.path);
      expect(response?.status(), `${page.path} should return 200`).toBe(200);

      // :visible, not a plain "h1" count — Clerk's own (hidden) heading inside
      // its sign-in/sign-up card is intentionally suppressed via display:none
      // rather than removed from the DOM, and a hidden heading is correctly
      // invisible to assistive tech too, so it shouldn't count against this.
      const h1 = browserPage.locator("h1:visible");
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
