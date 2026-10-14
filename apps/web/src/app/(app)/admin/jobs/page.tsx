"use client";

import { FileText } from "lucide-react";
import { EmptyState } from "@/components/ui/empty-state";
import { DashboardShell } from "@/components/dashboard/dashboard-shell";
import { ADMIN_NAV } from "../_nav";

// No platform-wide job-moderation backend exists yet (job CRUD is recruiter-owned, under
// /recruiter/jobs) — this page exists so the nav link lands somewhere honest instead of a
// 404, rather than inventing moderation UI with nothing behind it. See _nav.ts's `comingSoon`
// flag for the matching nav treatment.
export default function AdminJobsPage() {
  return (
    <DashboardShell nav={ADMIN_NAV} navLabel="Admin dashboard">
      <div className="space-y-6">
        <div className="space-y-1">
          <h1 className="text-h2 text-foreground">Jobs</h1>
          <p className="text-body text-muted-foreground">Review job listings across the platform.</p>
        </div>

        <EmptyState
          icon={<FileText className="size-6" />}
          title="Platform-wide job moderation isn't available yet"
          description="This feature hasn't been built. Nothing is being listed or moderated here — recruiters still manage their own listings as usual."
        />
      </div>
    </DashboardShell>
  );
}
