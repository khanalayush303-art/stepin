import { test, expect } from "@playwright/test";

test("jobs renders its loading state", async ({ page }) => {
  await page.goto("/jobs?state=loading");
  await expect(page.getByRole("status")).toContainText("Loading roles");
});

test("jobs renders its empty state with a way out", async ({ page }) => {
  await page.goto("/jobs?state=empty");
  await expect(page.getByRole("heading", { name: /no roles match those filters/i })).toBeVisible();
  await expect(page.getByRole("button", { name: "Clear all filters" })).toBeVisible();
});

test("jobs renders its error state as an alert", async ({ page }) => {
  await page.goto("/jobs?state=error");
  // Next.js mounts its own route announcer with role="alert"; scope to the page body.
  await expect(page.getByRole("alert").filter({ hasText: /could not load these roles/ })).toBeVisible();
  await expect(page.getByRole("button", { name: "Try again" })).toBeVisible();
});

test("filtering narrows results and can be cleared", async ({ page }) => {
  await page.goto("/jobs");
  // Filters collapse behind a disclosure below the lg breakpoint.
  if ((page.viewportSize()?.width ?? 0) < 1024) {
    await page.getByRole("button", { name: /show filters/i }).click();
  }
  await page.getByLabel("1-2 years").check();
  await expect(page.getByRole("status").or(page.getByText(/Showing 1 of 1 roles/))).toBeVisible();
  await page.getByRole("button", { name: "Clear all" }).click();
  await expect(page.getByText(/Showing 4 of 5 roles/)).toBeVisible();
});

// The form this exercised (sign-in) is now Clerk's own <SignIn/> component —
// its validation markup isn't ours to assert against. Clerk's rendered output
// is still covered by the WCAG scan in e2e/accessibility.spec.ts.
