import { auth } from "@clerk/nextjs/server";
import { requireAppUserWithRole } from "@/lib/auth/server";

export default async function AdminLayout({ children }: { children: React.ReactNode }) {
  await auth.protect({ unauthenticatedUrl: "/sign-in?returnTo=/admin" });
  await requireAppUserWithRole("Admin", "/sign-in?returnTo=/admin");

  return children;
}
