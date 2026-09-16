import { redirect } from "next/navigation";
import { auth } from "@clerk/nextjs/server";
import { getCurrentAppUser } from "@/lib/auth/server";
import { dashboardPathForRole } from "@/lib/auth/types";

export default async function AccountSetupLayout({ children }: { children: React.ReactNode }) {
  await auth.protect({ unauthenticatedUrl: "/sign-in?returnTo=/account-setup" });

  const user = await getCurrentAppUser();

  if (user?.role) {
    redirect(dashboardPathForRole(user.role));
  }

  return children;
}
