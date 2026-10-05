import { test, expect } from "@playwright/test";

/**
 * Admin AIDX opportunity routes. Automated coverage stops at the authentication boundary: Clerk
 * blocks scripted sign-in, so the admin screens themselves are verified manually (see the Phase
 * 4.4D checkpoint). These tests check that the routes are protected and that nothing is exposed
 * to an anonymous visitor.
 */

const PROTECTED_ADMIN_AIDX_ROUTES = [
  "/admin/aidx/opportunities",
  "/admin/aidx/opportunities/new",
  "/admin/aidx/opportunities/00000000-0000-0000-0000-000000000000/edit",
  "/admin/aidx/research",
  "/admin/aidx/research/new",
  "/admin/aidx/research/00000000-0000-0000-0000-000000000000/edit",
  "/admin/aidx/projects",
  "/admin/aidx/projects/new",
  "/admin/aidx/projects/00000000-0000-0000-0000-000000000000/edit",
  "/admin/aidx/people",
  "/admin/aidx/people/new",
  "/admin/aidx/people/00000000-0000-0000-0000-000000000000/edit",
  "/admin/aidx/publications",
  "/admin/aidx/publications/new",
  "/admin/aidx/publications/00000000-0000-0000-0000-000000000000/edit",
  "/admin/aidx/news",
  "/admin/aidx/news/new",
  "/admin/aidx/news/00000000-0000-0000-0000-000000000000/edit",
  "/admin/aidx/events",
  "/admin/aidx/events/new",
  "/admin/aidx/events/00000000-0000-0000-0000-000000000000/edit",
];

for (const path of PROTECTED_ADMIN_AIDX_ROUTES) {
  test(`${path} redirects an anonymous visitor to sign-in`, async ({ page }) => {
    await page.goto(path);
    await expect(page).toHaveURL(new RegExp(`/sign-in`));
    await expect(page.getByText("Research opportunities", { exact: true })).toHaveCount(0);
  });
}

test("the admin navigation source is served only behind admin protection", async ({ page }) => {
  const response = await page.goto("/admin/aidx/opportunities");
  expect(response?.status()).toBeLessThan(500);
  await expect(page.locator("main")).not.toContainText("Create research opportunity");
});
