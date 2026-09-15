"use client";

import {
  AlertCircle,
  BadgeCheck,
  Building2,
  FileText,
  LayoutDashboard,
  ScrollText,
  ShieldCheck,
  TrendingUp,
  Users,
} from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { DashboardShell, type DashboardNavItem } from "@/components/dashboard/dashboard-shell";
import { StatCard } from "@/components/dashboard/stat-card";

const NAV: DashboardNavItem[] = [
  { href: "/admin", label: "Overview", icon: LayoutDashboard },
  { href: "/admin/users", label: "Users", icon: Users },
  { href: "/admin/companies", label: "Companies", icon: Building2 },
  { href: "/admin/jobs", label: "Jobs", icon: FileText },
  { href: "/admin/verification", label: "Verification", icon: ShieldCheck, badge: "7" },
  { href: "/admin/reports", label: "Reports", icon: TrendingUp },
  { href: "/admin/audit", label: "Audit log", icon: ScrollText },
];

const VERIFICATION_QUEUE = [
  { id: "v1", company: "Northbridge Analytics", submitted: "2 days ago", check: "ABN matches trading name", state: "Needs review" },
  { id: "v2", company: "Harbour Health Group", submitted: "3 days ago", check: "Domain email pending", state: "Waiting on employer" },
  { id: "v3", company: "Wattle Consulting", submitted: "5 days ago", check: "First listing review", state: "Needs review" },
];

const FLAGGED = [
  { id: "f1", item: "Listing: 'Immediate start, no experience'", reason: "Salary omitted", severity: "warning" as const },
  { id: "f2", item: "Company: Bright Future Recruiting", reason: "Reported by 3 applicants", severity: "error" as const },
];

const AUDIT = [
  ["09:41", "a.khanal", "Approved verification for Woolworths Group"],
  ["09:02", "system", "Nightly job expiry ran — 12 listings closed"],
  ["Yesterday", "m.lee", "Suspended listing #4821 pending review"],
  ["Yesterday", "a.khanal", "Updated verification policy text"],
];

export default function AdminDashboardPage() {
  return (
    <DashboardShell nav={NAV} navLabel="Admin dashboard">
      <div className="space-y-6">
        <div className="space-y-1">
          <h1 className="text-h2 text-foreground">Platform overview</h1>
          <p className="text-body text-muted-foreground">
            7 verifications waiting, 2 flagged items, no incidents in the last 24 hours.
          </p>
        </div>

        <ul className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
          <li><StatCard icon={Users} label="Active users" value="8,412" delta="Up 312 this month" deltaTone="success" /></li>
          <li><StatCard icon={Building2} label="Companies" value="128" delta="6 awaiting verification" deltaTone="warning" /></li>
          <li><StatCard icon={FileText} label="Live listings" value="471" delta="Up 24 this week" deltaTone="success" /></li>
          <li><StatCard icon={TrendingUp} label="Applications (7d)" value="3,908" delta="Steady on last week" deltaTone="info" /></li>
        </ul>

        <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_340px]">
          <div className="space-y-6">
            <Card className="p-6">
              <Tabs defaultValue="verification">
                <TabsList aria-label="Moderation queues">
                  <TabsTrigger value="verification">
                    Verification ({VERIFICATION_QUEUE.length})
                  </TabsTrigger>
                  <TabsTrigger value="flagged">Flagged ({FLAGGED.length})</TabsTrigger>
                </TabsList>

                <TabsContent value="verification">
                  <ul className="divide-y divide-border">
                    {VERIFICATION_QUEUE.map((row) => (
                      <li key={row.id} className="flex flex-wrap items-center gap-4 py-3 first:pt-0">
                        <div className="min-w-0 flex-1">
                          <p className="text-small font-medium text-foreground">{row.company}</p>
                          <p className="text-caption text-muted-foreground">
                            {row.check} &middot; submitted {row.submitted}
                          </p>
                        </div>
                        <Badge tone={row.state === "Needs review" ? "warning" : "info"} showDot>
                          {row.state}
                        </Badge>
                        <Button variant="outline" size="sm">
                          Open
                        </Button>
                      </li>
                    ))}
                  </ul>
                </TabsContent>

                <TabsContent value="flagged">
                  <ul className="divide-y divide-border">
                    {FLAGGED.map((row) => (
                      <li key={row.id} className="flex flex-wrap items-center gap-4 py-3 first:pt-0">
                        <AlertCircle
                          className={
                            row.severity === "error"
                              ? "size-4 shrink-0 text-error"
                              : "size-4 shrink-0 text-warning"
                          }
                          aria-hidden="true"
                        />
                        <div className="min-w-0 flex-1">
                          <p className="text-small font-medium text-foreground">{row.item}</p>
                          <p className="text-caption text-muted-foreground">{row.reason}</p>
                        </div>
                        <Badge tone={row.severity} showDot>
                          {row.severity === "error" ? "Urgent" : "Review"}
                        </Badge>
                        <Button variant="outline" size="sm">
                          Open
                        </Button>
                      </li>
                    ))}
                  </ul>
                </TabsContent>
              </Tabs>
            </Card>

            <Card className="space-y-4 p-6">
              <h2 className="text-h4 text-foreground">Audit activity</h2>
              <ul className="divide-y divide-border">
                {AUDIT.map(([when, actor, action]) => (
                  <li key={`${when}-${action}`} className="flex flex-wrap items-baseline gap-x-4 gap-y-1 py-2.5 first:pt-0">
                    <span className="w-20 shrink-0 text-caption text-muted-foreground">{when}</span>
                    <span className="w-24 shrink-0 text-caption font-medium text-foreground">{actor}</span>
                    <span className="min-w-0 flex-1 text-small text-muted-foreground">{action}</span>
                  </li>
                ))}
              </ul>
            </Card>
          </div>

          <div className="space-y-6">
            <Card className="space-y-3 p-6">
              <div className="flex items-center gap-2">
                <BadgeCheck className="size-5 text-accent" aria-hidden="true" />
                <h2 className="text-h4 text-foreground">Verification health</h2>
              </div>
              <dl className="space-y-2.5 text-small">
                <div className="flex justify-between gap-3">
                  <dt className="text-muted-foreground">Verified companies</dt>
                  <dd className="font-medium text-foreground">122 of 128</dd>
                </div>
                <div className="flex justify-between gap-3">
                  <dt className="text-muted-foreground">Median review time</dt>
                  <dd className="font-medium text-foreground">1.8 days</dd>
                </div>
                <div className="flex justify-between gap-3">
                  <dt className="text-muted-foreground">Expiring in 30 days</dt>
                  <dd className="font-medium text-foreground">9</dd>
                </div>
              </dl>
            </Card>

            <Card className="space-y-3 p-6">
              <h2 className="text-h4 text-foreground">Reports</h2>
              <ul className="space-y-2">
                {[
                  "Applications by field — September",
                  "Employer response times — Q3",
                  "Listings closed without outcome",
                  "Accessibility issue reports",
                ].map((report) => (
                  <li key={report}>
                    <Button variant="link" size="sm" className="h-auto justify-start p-0 text-left">
                      {report}
                    </Button>
                  </li>
                ))}
              </ul>
            </Card>
          </div>
        </div>
      </div>
    </DashboardShell>
  );
}
