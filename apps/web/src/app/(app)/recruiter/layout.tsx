import { auth } from "@clerk/nextjs/server";
import { requireAppUserWithRole } from "@/lib/auth/server";

export default async function RecruiterLayout({ children }: { children: React.ReactNode }) {
  await auth.protect({ unauthenticatedUrl: "/sign-in?returnTo=/recruiter" });
  await requireAppUserWithRole("Recruiter", "/sign-in?returnTo=/recruiter");

  return children;
}
