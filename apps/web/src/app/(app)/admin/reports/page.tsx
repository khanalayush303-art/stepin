"use client";

import { TrendingUp } from "lucide-react";
import { EmptyState } from "@/components/ui/empty-state";
import { DashboardShell } from "@/components/dashboard/dashboard-shell";
import { ADMIN_NAV } from "../_nav";

// No reporting/analytics backend exists yet — this page exists so the nav link lands
// somewhere honest instead of a 404, rather than inventing charts with fabricated numbers.
// See _nav.ts's `comingSoon` flag for the matching nav treatment.
export default function AdminReportsPage() {
  return (
    <DashboardShell nav={ADMIN_NAV} navLabel="Admin dashboard">
      <div className="space-y-6">
        <div className="space-y-1">
          <h1 className="text-h2 text-foreground">Reports</h1>
          <p className="text-body text-muted-foreground">Platform-wide usage and outcome reporting.</p>
        </div>

        <EmptyState
          icon={<TrendingUp className="size-6" />}
          title="Reporting isn't available yet"
          description="This feature hasn't been built. No platform-wide metrics are being tracked or shown here."
        />
      </div>
    </DashboardShell>
  );
}
