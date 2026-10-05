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
