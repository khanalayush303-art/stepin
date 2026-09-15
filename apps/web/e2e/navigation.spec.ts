import { test, expect } from "@playwright/test";

/** The desktop nav appears at the lg breakpoint (1024px), not at a device class. */
async function isCompact(page: import("@playwright/test").Page) {
  return (page.viewportSize()?.width ?? 0) < 1024;
}

test("primary navigation reaches every public surface", async ({ page }) => {
  await page.goto("/");

  if (await isCompact(page)) {
    await page.getByRole("button", { name: "Open menu" }).click();
    await expect(page.getByRole("navigation", { name: "Primary mobile" })).toBeVisible();
    await page.getByRole("navigation", { name: "Primary mobile" }).getByRole("link", { name: "Jobs" }).click();
  } else {
    await page.getByRole("navigation", { name: "Primary" }).getByRole("link", { name: "Jobs" }).click();
  }

  await expect(page).toHaveURL(/\/jobs$/);
  await expect(page.getByRole("heading", { level: 1 })).toContainText("Graduate & entry-level jobs");
});

test("the current route is marked with aria-current, not colour alone", async ({ page }) => {
  await page.goto("/jobs");
  test.skip(await isCompact(page), "The underline marker is a desktop-nav affordance.");
  const current = page.getByRole("navigation", { name: "Primary" }).locator('[aria-current="page"]');
  await expect(current).toHaveCount(1);
  await expect(current).toContainText("Jobs");
});

test("a job card links through to its detail page", async ({ page }) => {
  await page.goto("/jobs");
  await page.getByRole("link", { name: "Junior Data Analyst" }).first().click();
  await expect(page).toHaveURL(/\/jobs\/junior-data-analyst-atlassian$/);
  await expect(page.getByRole("heading", { level: 1 })).toContainText("Junior Data Analyst");
});

test("skip link is the first focusable element", async ({ page, isMobile }) => {
  test.skip(isMobile, "Keyboard tabbing is not meaningful on the touch profile.");
  await page.goto("/");
  await page.keyboard.press("Tab");
  await expect(page.getByRole("link", { name: "Skip to main content" })).toBeFocused();
});
