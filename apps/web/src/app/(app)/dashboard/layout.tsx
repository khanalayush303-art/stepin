import { auth } from "@clerk/nextjs/server";
import { requireAppUserWithRole } from "@/lib/auth/server";

export default async function DashboardLayout({ children }: { children: React.ReactNode }) {
  await auth.protect({ unauthenticatedUrl: "/sign-in?returnTo=/dashboard" });
  await requireAppUserWithRole("Applicant", "/sign-in?returnTo=/dashboard");

  return children;
}
