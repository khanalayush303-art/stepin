import { redirect } from "next/navigation";
import { auth } from "@clerk/nextjs/server";
import { getCurrentAppUser } from "@/lib/auth/server";
import { dashboardPathForRole } from "@/lib/auth/types";

export default async function DashboardLayout({ children }: { children: React.ReactNode }) {
  await auth.protect({ unauthenticatedUrl: "/sign-in?returnTo=/dashboard" });

  const user = await getCurrentAppUser();

  if (!user?.role) {
    redirect("/account-setup");
  }

  if (user.role !== "Applicant") {
    redirect(dashboardPathForRole(user.role));
  }

  return children;
}
