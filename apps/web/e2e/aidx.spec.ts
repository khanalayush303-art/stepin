import { test, expect } from "@playwright/test";
import AxeBuilder from "@axe-core/playwright";

/**
 * Public AIDX Lab. These tests run without a backend in some environments, so they assert
 * the page shell and its honest states (error or empty), never specific records.
 */

const PUBLIC_ROUTES = [
  "/aidx",
  "/aidx/research",
  "/aidx/projects",
  "/aidx/people",
  "/aidx/publications",
  "/aidx/news",
  "/aidx/events",
  "/aidx/opportunities",
  "/aidx/about",
  "/aidx/contact",
];

test.describe("AIDX public pages", () => {
  for (const path of PUBLIC_ROUTES) {
    test(`${path} is public, renders one h1 and does not scroll sideways`, async ({ page }) => {
      const response = await page.goto(path);
      expect(response?.status(), `${path} must be served`).toBe(200);

      // Public means no Clerk redirect to sign-in.
      expect(new URL(page.url()).pathname).toBe(path);

      await expect(page.locator("h1:visible")).toHaveCount(1);

      const overflow = await page.evaluate(
        () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
      );
      expect(overflow, `${path} must not scroll horizontally`).toBeLessThanOrEqual(1);
    });

    test(`${path} passes an accessibility scan`, async ({ page }) => {
      await page.goto(path);
      const results = await new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa", "wcag21a", "wcag21aa"]).analyze();
      expect(results.violations, JSON.stringify(results.violations, null, 2)).toEqual([]);
    });
  }
});

test.describe("AIDX navigation", () => {
  test("the AIDX section navigation links every public section", async ({ page }) => {
    await page.goto("/aidx");
    const nav = page.getByRole("navigation", { name: "AIDX Lab" });
    for (const label of ["Research", "Projects", "People", "Publications", "News", "Events", "Opportunities", "About", "Contact"]) {
      await expect(nav.getByRole("link", { name: label, exact: true })).toBeVisible();
    }
  });

  test("the StepIn header links into AIDX Lab", async ({ page }) => {
    await page.goto("/");
    // The primary navigation is shown from the large breakpoint up. Below it, the link lives in the
    // menu, which renders its items only once opened, so open it first, as a visitor would.
    const viewport = page.viewportSize();
    if (viewport && viewport.width < 1024) {
      await page.getByRole("button", { name: "Open menu" }).click();
      await expect(page.getByRole("link", { name: "AIDX Lab" }).first()).toBeVisible();
    } else {
      await expect(page.getByRole("navigation", { name: "Primary" }).getByRole("link", { name: "AIDX Lab" })).toBeVisible();
    }
  });
});

test.describe("AIDX opportunities", () => {
  test("every opportunity card links to the StepIn job detail, never to an AIDX-specific page", async ({ page }) => {
    await page.goto("/aidx/opportunities");
    const cardLinks = page.locator("main a[href^='/dashboard/discover/']");
    const count = await cardLinks.count();
    for (let index = 0; index < count; index += 1) {
      const href = await cardLinks.nth(index).getAttribute("href");
      expect(href).toMatch(/^\/dashboard\/discover\/[0-9a-f-]{36}$/);
    }
    await expect(page.locator("main a[href^='/aidx/opportunities/']")).toHaveCount(0);
  });
});

test.describe("AIDX detail routes", () => {
  test("an unknown or unavailable project does not crash the page", async ({ page }) => {
    const response = await page.goto("/aidx/projects/this-slug-does-not-exist");
    // With the API running this is a 404. Without it, the page shows the error state. Either way it must not be a 500.
    expect([200, 404]).toContain(response?.status());
    await expect(page.locator("h1:visible, h2:visible").first()).toBeVisible();
  });

  test("an unknown news item is not served as a server error", async ({ page }) => {
    const response = await page.goto("/aidx/news/this-slug-does-not-exist");
    expect([200, 404]).toContain(response?.status());
  });
});
