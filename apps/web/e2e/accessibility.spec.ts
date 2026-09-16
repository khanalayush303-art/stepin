import { test, expect } from "@playwright/test";
import AxeBuilder from "@axe-core/playwright";

const ROUTES = [
  "/",
  "/jobs",
  "/internships",
  "/companies",
  "/how-it-works",
  "/about",
  "/sign-in",
  "/register",
  // forgot-password/reset-password/verify-email redirect to /sign-in; /dashboard,
  // /recruiter, /admin and /account-setup require a signed-in session — both
  // covered by e2e/auth.spec.ts instead.
];

for (const route of ROUTES) {
  test(`${route} has no WCAG 2.1 A/AA violations`, async ({ page }) => {
    await page.goto(route);
    const results = await new AxeBuilder({ page })
      .withTags(["wcag2a", "wcag2aa", "wcag21a", "wcag21aa"])
      .analyze();

    expect(
      results.violations.map((v) => `${v.id}: ${v.nodes.length} node(s) — ${v.help}`)
    ).toEqual([]);
  });
}
