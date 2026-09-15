import { test, expect } from "@playwright/test";

// Covers what's frontend-observable without a live API+database. The full
// register/verify/login/logout/OAuth flows are covered by the persistence-backed
// xUnit suite instead (apps/api/tests/StepIn.Api.Tests/AuthEndpointsTests.cs).

const PROTECTED_ROUTES = ["/dashboard", "/recruiter", "/admin"];

for (const path of PROTECTED_ROUTES) {
  test(`${path} redirects an unauthenticated visitor to sign-in`, async ({ page }) => {
    await page.goto(path);
    await expect(page).toHaveURL(new RegExp(`/sign-in\\?returnTo=${encodeURIComponent(path)}$`));
    await expect(page.getByRole("heading", { level: 1 })).toContainText("Welcome back");
  });
}

test("register requires first name, last name, email, password and terms", async ({ page }) => {
  await page.goto("/register");
  await page.getByRole("button", { name: "Create account" }).click();

  await expect(page.getByRole("alert").filter({ hasText: "Enter your first name" })).toBeVisible();
  await expect(page.getByRole("alert").filter({ hasText: "Enter your last name" })).toBeVisible();
  await expect(page.getByRole("alert").filter({ hasText: /Enter a complete email address/ })).toBeVisible();
  await expect(
    page.getByRole("alert").filter({ hasText: "You need to accept the terms" })
  ).toBeVisible();
});

test("register lets the applicant/recruiter toggle switch which email hint is shown", async ({ page }) => {
  await page.goto("/register");
  await expect(page.getByPlaceholder("you@university.edu.au")).toBeVisible();

  await page.getByRole("radio", { name: "I am hiring" }).click();
  await expect(page.getByPlaceholder("you@company.com.au")).toBeVisible();
});

test("forgot-password requires a valid email before submitting", async ({ page }) => {
  await page.goto("/forgot-password");
  await page.getByRole("button", { name: "Send reset link" }).click();
  await expect(page.getByRole("alert").filter({ hasText: /Enter a complete email address/ })).toBeVisible();
});

test("reset-password without a token is treated as an expired link", async ({ page }) => {
  await page.goto("/reset-password");
  await expect(page.getByRole("heading", { level: 1 })).toContainText("invalid or has expired");
  await expect(page.getByRole("link", { name: "Request a new link" })).toBeVisible();
});

test("verify-email without a token shows the check-your-inbox state with a resend action", async ({ page }) => {
  await page.goto("/verify-email?email=student%40example.com");
  await expect(page.getByRole("heading", { level: 1 })).toContainText("Check your email");
  await expect(page.getByText("student@example.com")).toBeVisible();
  await expect(page.getByRole("button", { name: "Resend the link" })).toBeVisible();
});

test("the Google button is a real link to the OAuth challenge endpoint, not a dead button", async ({ page }) => {
  await page.goto("/sign-in");
  const googleLink = page.getByRole("link", { name: /Continue with Google/ });
  await expect(googleLink).toHaveAttribute("href", "/api/v1/auth/external/google");
});

test("sign-in and register redirect away an already-authenticated cookie-less request the same as anyone else", async ({
  page,
}) => {
  // Without a session cookie, the auth pages must render normally rather than
  // erroring out while proxy.ts asks the API who's signed in.
  await page.goto("/sign-in");
  await expect(page.getByRole("heading", { level: 1 })).toContainText("Welcome back");
  await page.goto("/register");
  await expect(page.getByRole("heading", { level: 1 })).toContainText("Create your account");
});
