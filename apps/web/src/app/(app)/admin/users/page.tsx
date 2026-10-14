import { Users } from "lucide-react";
import { EmptyState } from "@/components/ui/empty-state";
import { DashboardShell } from "@/components/dashboard/dashboard-shell";
import { ADMIN_NAV } from "../_nav";

// No user-management backend exists yet (no list/suspend/role-change endpoint) — this page
// exists so the nav link (and any bookmark to this URL) lands somewhere honest instead of a
// 404, rather than inventing management UI with nothing behind it. See _nav.ts's `comingSoon`
// flag for the matching nav treatment.
export default function AdminUsersPage() {
  return (
    <DashboardShell nav={ADMIN_NAV} navLabel="Admin dashboard">
      <div className="space-y-6">
        <div className="space-y-1">
          <h1 className="text-h2 text-foreground">Users</h1>
          <p className="text-body text-muted-foreground">Manage candidate and recruiter accounts.</p>
        </div>

        <EmptyState
          icon={<Users className="size-6" />}
          title="User management isn't available yet"
          description="This feature hasn't been built. Nothing is being listed or managed here — no accounts are affected."
        />
      </div>
    </DashboardShell>
  );
}
