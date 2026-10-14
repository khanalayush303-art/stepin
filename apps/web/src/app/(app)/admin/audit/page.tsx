import { ScrollText } from "lucide-react";
import { EmptyState } from "@/components/ui/empty-state";
import { DashboardShell } from "@/components/dashboard/dashboard-shell";
import { ADMIN_NAV } from "../_nav";

// No audit-log backend exists yet (no persisted record of admin/moderation actions) — this
// page exists so the nav link lands somewhere honest instead of a 404, rather than inventing
// a log with fabricated entries. See _nav.ts's `comingSoon` flag for the matching nav treatment.
export default function AdminAuditPage() {
  return (
    <DashboardShell nav={ADMIN_NAV} navLabel="Admin dashboard">
      <div className="space-y-6">
        <div className="space-y-1">
          <h1 className="text-h2 text-foreground">Audit log</h1>
          <p className="text-body text-muted-foreground">A record of administrative actions on StepIn.</p>
        </div>

        <EmptyState
          icon={<ScrollText className="size-6" />}
          title="The audit log isn't available yet"
          description="This feature hasn't been built. No administrative actions are being recorded here."
        />
      </div>
    </DashboardShell>
  );
}
