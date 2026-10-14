import { requireAidxManageAccess } from "@/lib/aidx/membership";

export default async function AidxManageLayout({ children }: { children: React.ReactNode }) {
  await requireAidxManageAccess("/sign-in?returnTo=/aidx/manage");

  return children;
}
