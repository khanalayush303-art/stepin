import { Bell } from "lucide-react";
import { EmptyState } from "@/components/ui/empty-state";
import { CANDIDATE_NAV } from "@/components/dashboard/candidate-nav";
import { DashboardShell } from "@/components/dashboard/dashboard-shell";

const NAV = CANDIDATE_NAV;

// There is no notification system behind this route yet — no backend endpoint, no stored
// preferences, no delivery mechanism. This page exists so the nav link (and any bookmark to
// this URL) lands somewhere honest instead of a 404, and so nothing here ever claims a feature
// that isn't built. See candidate-nav.ts's `comingSoon` flag for the matching nav treatment.
export default function NotificationsPage() {
  return (
    <DashboardShell nav={NAV} navLabel="Applicant dashboard">
      <div className="space-y-6">
        <div className="space-y-1">
          <h1 className="text-h2 text-foreground">Notifications</h1>
          <p className="text-body text-muted-foreground">
            Updates about your applications, matches, and account.
          </p>
        </div>

        <EmptyState
          icon={<Bell className="size-6" />}
          title="Notifications aren't available yet"
          description="This feature hasn't been built. Nothing is being tracked or delivered here — check your applications, saved roles, and matches directly for now."
        />
      </div>
    </DashboardShell>
  );
}
