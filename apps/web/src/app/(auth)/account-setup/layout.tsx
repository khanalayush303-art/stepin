import { redirect } from "next/navigation";
import { auth } from "@clerk/nextjs/server";
import { getCurrentAppUserResult } from "@/lib/auth/server";
import { dashboardPathForRole } from "@/lib/auth/types";

export default async function AccountSetupLayout({ children }: { children: React.ReactNode }) {
  await auth.protect({ unauthenticatedUrl: "/sign-in?returnTo=/account-setup" });

  const result = await getCurrentAppUserResult();

  switch (result.status) {
    case "signed-out":
      redirect("/sign-in?returnTo=/account-setup");
      break;
    case "unavailable":
      redirect("/service-unavailable");
      break;
    case "disabled":
      // A disabled account can never acquire a role through this form — the backend
      // rejects the attempt anyway, but there's no reason to show the form at all.
      redirect("/account-disabled");
      break;
    case "active":
      redirect(dashboardPathForRole(result.user.role!));
      break;
    case "no-role":
      // The only status this page is actually for.
      break;
  }

  return children;
}
