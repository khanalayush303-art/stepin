"use client";

import Link from "next/link";
import {
  BadgeCheck,
  Bell,
  Bookmark,
  Clock,
  FileText,
  GraduationCap,
  LayoutDashboard,
  Search,
  User,
} from "lucide-react";
import { Avatar } from "@/components/ui/avatar";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { StatusBadge } from "@/components/ui/status-badge";
import { ApplicationStatusIndicator } from "@/components/jobs/application-status-indicator";
import { DashboardShell, type DashboardNavItem } from "@/components/dashboard/dashboard-shell";
import { StatCard } from "@/components/dashboard/stat-card";
import { APPLICATIONS, INTERVIEWS, JOBS, NOTIFICATIONS } from "@/lib/placeholder-data";
import { formatDate } from "@/lib/utils";

const NAV: DashboardNavItem[] = [
  { href: "/dashboard", label: "Overview", icon: LayoutDashboard },
  { href: "/dashboard/discover", label: "Discover roles", icon: Search },
  { href: "/dashboard/applications", label: "Applications", icon: FileText, badge: "12" },
  { href: "/dashboard/saved", label: "Saved roles", icon: Bookmark, badge: "8" },
  { href: "/dashboard/interviews", label: "Interviews", icon: User, badge: "2" },
  { href: "/dashboard/prepare", label: "Prepare", icon: GraduationCap },
  { href: "/dashboard/notifications", label: "Notifications", icon: Bell, badge: "3" },
];

const NOTIFICATION_ICON = {
  info: Clock,
  success: BadgeCheck,
  warning: Clock,
  error: Bell,
} as const;

const NOTIFICATION_TONE = {
  info: "text-info",
  success: "text-success",
  warning: "text-warning",
  error: "text-error",
} as const;

export default function ApplicantDashboardPage() {
  const active = APPLICATIONS[0];
  const recent = APPLICATIONS.slice(1);
  const recommended = JOBS.slice(4, 7);
  const completion = 60;

  return (
    <DashboardShell nav={NAV} navLabel="Applicant dashboard">
      <div className="space-y-6">
        <div className="flex flex-wrap items-center justify-between gap-4">
          <div className="space-y-1">
            <h1 className="text-h2 text-foreground">Good morning, Ayush</h1>
            <p className="text-body text-muted-foreground">
              You have 2 interviews this week and 3 roles closing soon.
            </p>
          </div>
          <div className="flex items-center gap-3">
            <Button asChild>
              <Link href="/jobs">
                <Search />
                Find roles
              </Link>
            </Button>
            <Avatar name="Ayush Khanal" />
          </div>
        </div>

        {/* Profile completion */}
        <Card className="border-0 bg-primary-subtle p-6">
          <div className="flex flex-col gap-4 lg:flex-row lg:items-center">
            <div className="flex-1 space-y-2.5">
              <h2 className="text-h4 text-primary-subtle-fg">
                Your profile is {completion}% complete
              </h2>
              <div
                className="h-2 w-full overflow-hidden rounded-full bg-surface"
                role="progressbar"
                aria-valuenow={completion}
                aria-valuemin={0}
                aria-valuemax={100}
                aria-label="Profile completion"
              >
                <span
                  className="block h-full rounded-full bg-primary"
                  style={{ width: `${completion}%` }}
                />
              </div>
              <p className="text-small text-primary-subtle-fg">
                Add your résumé and two more skills to appear in employer searches.
              </p>
            </div>
            <Button className="lg:shrink-0">Complete profile</Button>
          </div>
        </Card>

        {/* Metrics */}
        <ul className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
          <li>
            <StatCard icon={FileText} label="Applications" value="12" delta="Up 3 this week" deltaTone="success" />
          </li>
          <li>
            <StatCard icon={Clock} label="In review" value="5" delta="2 since Monday" deltaTone="info" />
          </li>
          <li>
            <StatCard icon={User} label="Interviews" value="2" delta="Next: Thursday" deltaTone="brand" />
          </li>
          <li>
            <StatCard icon={Bookmark} label="Saved roles" value="8" delta="3 closing soon" deltaTone="warning" />
          </li>
        </ul>

        <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_340px]">
          <div className="space-y-6">
            {/* Active application */}
            <Card className="space-y-4 p-6">
              <div className="flex flex-wrap items-center justify-between gap-3">
                <div>
                  <h2 className="text-h4 text-foreground">{active.jobTitle}</h2>
                  <p className="text-small text-muted-foreground">
                    {active.companyName} &middot; applied 2 September
                  </p>
                </div>
                <Button variant="link" size="sm" className="h-auto p-0" asChild>
                  <Link href="/dashboard/applications">View application</Link>
                </Button>
              </div>
              <ApplicationStatusIndicator stages={active.stages} />
            </Card>

            {/* Recent applications */}
            <Card className="space-y-4 p-6">
              <h2 className="text-h4 text-foreground">Recent applications</h2>
              <ul className="divide-y divide-border">
                {recent.map((application) => (
                  <li
                    key={application.id}
                    className="flex flex-wrap items-center gap-4 py-3 first:pt-0 last:pb-0"
                  >
                    <div className="min-w-0 flex-1">
                      <p className="truncate text-small font-medium text-foreground">
                        {application.jobTitle}
                      </p>
                      <p className="text-caption text-muted-foreground">
                        {application.companyName}
                      </p>
                    </div>
                    <StatusBadge status={application.status} />
                    <span className="w-20 text-right text-caption text-muted-foreground">
                      {formatDate(application.updatedAt)}
                    </span>
                  </li>
                ))}
              </ul>
            </Card>
          </div>

          <div className="space-y-6">
            <Card className="space-y-4 p-6">
              <h2 className="text-h4 text-foreground">Upcoming interviews</h2>
              <ul className="space-y-3">
                {INTERVIEWS.map((interview) => (
                  <li key={interview.id} className="rounded-md bg-muted p-4">
                    <p className="text-small font-medium text-foreground">{interview.jobTitle}</p>
                    <p className="text-caption text-muted-foreground">{interview.companyName}</p>
                    <p className="mt-1.5 flex items-center gap-1.5 text-caption text-primary">
                      <Clock className="size-3.5" aria-hidden="true" />
                      {interview.scheduledFor} &middot; {interview.mode} &middot;{" "}
                      {interview.durationMinutes} min
                    </p>
                  </li>
                ))}
              </ul>
            </Card>

            <Card className="space-y-4 p-6">
              <h2 className="text-h4 text-foreground">Recommended for you</h2>
              <ul className="space-y-3">
                {recommended.map((job, i) => (
                  <li key={job.id} className="space-y-1">
                    <Link
                      href={`/jobs/${job.slug}`}
                      className="rounded-sm text-small font-medium text-foreground hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
                    >
                      {job.title}
                    </Link>
                    <p className="flex items-center gap-2 text-caption text-muted-foreground">
                      {job.company.name}
                      <span className="rounded-sm bg-accent-subtle px-1.5 text-accent-subtle-fg">
                        {[92, 87, 81][i]}% match
                      </span>
                    </p>
                  </li>
                ))}
              </ul>
            </Card>

            <Card className="space-y-4 p-6">
              <h2 className="text-h4 text-foreground">Notifications</h2>
              <ul className="space-y-3">
                {NOTIFICATIONS.map((item) => {
                  const Icon = NOTIFICATION_ICON[item.tone];
                  return (
                    <li key={item.id} className="flex gap-2.5">
                      <Icon
                        className={`mt-0.5 size-4 shrink-0 ${NOTIFICATION_TONE[item.tone]}`}
                        aria-hidden="true"
                      />
                      <div>
                        <p className="text-small text-foreground">{item.message}</p>
                        <p className="text-caption text-muted-foreground">{item.at}</p>
                      </div>
                    </li>
                  );
                })}
              </ul>
            </Card>
          </div>
        </div>
      </div>
    </DashboardShell>
  );
}
