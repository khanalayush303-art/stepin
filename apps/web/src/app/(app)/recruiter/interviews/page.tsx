"use client";

import { Building2, Clock, FileText, LayoutDashboard, Users } from "lucide-react";
import { EmptyState } from "@/components/ui/empty-state";
import { DashboardShell, type DashboardNavItem } from "@/components/dashboard/dashboard-shell";

const NAV: DashboardNavItem[] = [
  { href: "/recruiter", label: "Overview", icon: LayoutDashboard },
  { href: "/recruiter/jobs", label: "Job listings", icon: FileText, badge: "6" },
  { href: "/recruiter/applications", label: "Applications", icon: Users },
  { href: "/recruiter/interviews", label: "Interviews", icon: Clock, comingSoon: true },
  { href: "/recruiter/profile", label: "Company profile", icon: Building2 },
];

// No interview-scheduling backend exists yet (InterviewEndpoints.cs is the candidate-only
// Interview Practice Studio, not recruiter scheduling) — this page exists so the nav link
// lands somewhere honest instead of a 404, rather than inventing a schedule with fabricated
// entries. See this file's own NAV array for the matching `comingSoon` nav treatment.
export default function RecruiterInterviewsPage() {
  return (
    <DashboardShell nav={NAV} navLabel="Recruiter dashboard">
      <div className="space-y-6">
        <div className="space-y-1">
          <h1 className="text-h2 text-foreground">Interviews</h1>
          <p className="text-body text-muted-foreground">Scheduled interviews with your candidates.</p>
        </div>

        <EmptyState
          icon={<Clock className="size-6" />}
          title="Interview scheduling isn't available yet"
          description="This feature hasn't been built. Nothing is being scheduled or tracked here — check Applications to review candidates directly for now."
        />
      </div>
    </DashboardShell>
  );
}
