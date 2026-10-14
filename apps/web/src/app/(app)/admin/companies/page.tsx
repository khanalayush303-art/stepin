"use client";

import { Building2 } from "lucide-react";
import { EmptyState } from "@/components/ui/empty-state";
import { DashboardShell } from "@/components/dashboard/dashboard-shell";
import { ADMIN_NAV } from "../_nav";

// No standalone company-management backend exists yet (employer verification lives under
// /admin/verification, which already works) — this page exists so the nav link lands
// somewhere honest instead of a 404, rather than inventing management UI with nothing
// behind it. See _nav.ts's `comingSoon` flag for the matching nav treatment.
export default function AdminCompaniesPage() {
  return (
    <DashboardShell nav={ADMIN_NAV} navLabel="Admin dashboard">
      <div className="space-y-6">
        <div className="space-y-1">
          <h1 className="text-h2 text-foreground">Companies</h1>
          <p className="text-body text-muted-foreground">Manage employer companies on StepIn.</p>
        </div>

        <EmptyState
          icon={<Building2 className="size-6" />}
          title="Company management isn't available yet"
          description="This feature hasn't been built. For employer verification, use Verification in the sidebar instead."
        />
      </div>
    </DashboardShell>
  );
}
