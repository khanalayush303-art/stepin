import { redirect } from "next/navigation";
import { requireAidxManageAccess } from "@/lib/aidx/membership";

/** Only LabManager (or platform Admin) can manage memberships — the permission matrix's "Manage
 * lab memberships" row. An Editor reaches /aidx/manage itself fine, but not this sub-page. */
export default async function AidxMembershipsLayout({ children }: { children: React.ReactNode }) {
  const access = await requireAidxManageAccess("/sign-in?returnTo=/aidx/manage/memberships");

  if (!access.isPlatformAdmin && access.labRole !== "LabManager") {
    redirect("/aidx/manage?reason=not-a-lab-manager");
  }

  return children;
}
