import { requireAidxWorkspaceAccess } from "@/lib/aidx/membership";

export default async function AidxWorkspaceLayout({ children }: { children: React.ReactNode }) {
  await requireAidxWorkspaceAccess("/sign-in?returnTo=/aidx/workspace");

  return children;
}
