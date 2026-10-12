"use client";

import * as React from "react";
import Link from "next/link";
import { ShieldCheck } from "lucide-react";
import { useAuth } from "@clerk/nextjs";
import { Alert } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { EmptyState } from "@/components/ui/empty-state";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import { DashboardShell } from "@/components/dashboard/dashboard-shell";
import { formError } from "@/lib/auth/client";
import { listExperienceQueue } from "@/lib/experience/api";
import type { ExperienceQueueItem, ExperienceVerificationStatus } from "@/lib/experience/types";
import { ADMIN_NAV } from "../_nav";

const STATUS_TONE: Record<ExperienceVerificationStatus, "neutral" | "info" | "success" | "error" | "warning"> = {
  Draft: "neutral",
  Submitted: "info",
  UnderReview: "warning",
  Verified: "success",
  Rejected: "error",
  Withdrawn: "neutral",
};

const FILTERS = ["all", "Submitted", "UnderReview"] as const;

export default function VerificationQueuePage() {
  const { getToken } = useAuth();
  const [loading, setLoading] = React.useState(true);
  const [loadError, setLoadError] = React.useState<string>();
  const [items, setItems] = React.useState<ExperienceQueueItem[]>([]);
  const [filter, setFilter] = React.useState<(typeof FILTERS)[number]>("all");

  const load = React.useCallback(
    async (status: (typeof FILTERS)[number]) => {
      setLoading(true);
      setLoadError(undefined);
      try {
        const token = await getToken();
        setItems(await listExperienceQueue(token, status === "all" ? undefined : status));
      } catch (error) {
        setLoadError(formError(error));
      } finally {
        setLoading(false);
      }
    },
    [getToken],
  );

  React.useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void load(filter);
  }, [load, filter]);

  return (
    <DashboardShell nav={ADMIN_NAV} navLabel="Admin dashboard">
      <div className="space-y-6">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div className="space-y-1">
            <h1 className="text-h2 text-foreground">Experience verification</h1>
            <p className="text-body text-muted-foreground">
              Review experience records candidates have submitted for verification.
            </p>
          </div>
          <div className="min-w-[180px]">
            <Select value={filter} onValueChange={(v) => setFilter(v as (typeof FILTERS)[number])}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {FILTERS.map((value) => (
                  <SelectItem key={value} value={value}>
                    {value === "all" ? "Submitted + under review" : value === "Submitted" ? "Submitted only" : "Under review only"}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        </div>

        {loading ? (
          <div className="space-y-3">
            <Skeleton className="h-16 w-full" />
            <Skeleton className="h-16 w-full" />
          </div>
        ) : loadError ? (
          <Alert tone="error" title="Couldn't load the verification queue">
            {loadError}
            <div className="mt-3">
              <Button type="button" variant="outline" size="sm" onClick={() => void load(filter)}>
                Try again
              </Button>
            </div>
          </Alert>
        ) : items.length === 0 ? (
          <EmptyState icon={<ShieldCheck className="size-6" />} title="Nothing waiting on review" description="New submissions will appear here." />
        ) : (
          <ul className="space-y-3">
            {items.map((item) => (
              <li key={item.id}>
                <Card interactive className="flex flex-wrap items-center gap-3 p-4">
                  <div className="min-w-0 flex-1 space-y-1">
                    <p className="text-caption font-medium uppercase tracking-wide text-muted-foreground">{item.recordType}</p>
                    <h2 className="text-body font-medium text-foreground">
                      <Link href={`/admin/verification/${item.id}`} className="rounded-sm after:absolute after:inset-0 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring">
                        {item.title}
                      </Link>
                    </h2>
                    <p className="text-small text-foreground-secondary">
                      {item.applicantName}
                      {item.organization ? ` · ${item.organization}` : ""}
                    </p>
                  </div>
                  <Badge tone={STATUS_TONE[item.status]} variant="subtle">
                    {item.status}
                  </Badge>
                  <Button size="sm" variant="outline" asChild className="relative z-10">
                    <Link href={`/admin/verification/${item.id}`} tabIndex={-1}>
                      Review
                    </Link>
                  </Button>
                </Card>
              </li>
            ))}
          </ul>
        )}
      </div>
    </DashboardShell>
  );
}
