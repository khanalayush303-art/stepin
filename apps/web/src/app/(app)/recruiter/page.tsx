"use client";

import Link from "next/link";
import {
  BadgeCheck,
  Building2,
  Clock,
  FileText,
  LayoutDashboard,
  Plus,
  TrendingUp,
  Users,
} from "lucide-react";
import { Avatar } from "@/components/ui/avatar";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { StatusBadge } from "@/components/ui/status-badge";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { DashboardShell, type DashboardNavItem } from "@/components/dashboard/dashboard-shell";
import { StatCard } from "@/components/dashboard/stat-card";
import type { ApplicationStatus } from "@/lib/types";

const NAV: DashboardNavItem[] = [
  { href: "/recruiter", label: "Overview", icon: LayoutDashboard },
  { href: "/recruiter/jobs", label: "Job listings", icon: FileText, badge: "6" },
  { href: "/recruiter/candidates", label: "Candidates", icon: Users, badge: "84" },
  { href: "/recruiter/interviews", label: "Interviews", icon: Clock, badge: "5" },
  { href: "/recruiter/company", label: "Company profile", icon: Building2 },
];

const LIVE_JOBS = [
  { id: "r1", title: "Junior Data Analyst", applicants: 34, newApplicants: 6, closes: "30 Sep", stage: "Live" },
  { id: "r2", title: "Graduate Software Engineer", applicants: 51, newApplicants: 11, closes: "5 Oct", stage: "Live" },
  { id: "r3", title: "Business Analyst — Graduate", applicants: 28, newApplicants: 2, closes: "12 Oct", stage: "Live" },
];

const DRAFT_JOBS = [
  { id: "d1", title: "Data Engineering Intern", updated: "Edited yesterday", missing: "Salary range" },
  { id: "d2", title: "Product Analyst", updated: "Edited 4 days ago", missing: "Closing date" },
];

const PIPELINE: { name: string; role: string; status: ApplicationStatus; when: string }[] = [
  { name: "Priya Sharma", role: "Junior Data Analyst", status: "Interview", when: "Today" },
  { name: "Tom Nguyen", role: "Graduate Software Engineer", status: "InReview", when: "Yesterday" },
  { name: "Aisha Rahman", role: "Junior Data Analyst", status: "Submitted", when: "2 days ago" },
  { name: "Liam O'Connor", role: "Business Analyst — Graduate", status: "Offer", when: "3 days ago" },
];

const ACTIVITY = [
  "Priya Sharma moved to Interview by you",
  "6 new applications for Junior Data Analyst",
  "Verification renewed for another 12 months",
  "Graduate Software Engineer listing was featured",
];

export default function RecruiterDashboardPage() {
  return (
    <DashboardShell nav={NAV} navLabel="Recruiter dashboard">
      <div className="space-y-6">
        <div className="flex flex-wrap items-center justify-between gap-4">
          <div className="space-y-1">
            <div className="flex flex-wrap items-center gap-3">
              <h1 className="text-h2 text-foreground">Atlassian</h1>
              <Badge tone="verified" icon={<BadgeCheck className="size-3.5" aria-hidden="true" />}>
                Verified employer
              </Badge>
            </div>
            <p className="text-body text-muted-foreground">
              6 live roles &middot; 84 candidates in your pipeline &middot; median response 3.1 days
            </p>
          </div>
          <div className="flex items-center gap-3">
            <Button>
              <Plus />
              Post a role
            </Button>
            <Avatar name="Recruiting Team" />
          </div>
        </div>

        <ul className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
          <li><StatCard icon={FileText} label="Live roles" value="6" delta="1 closes this week" deltaTone="warning" /></li>
          <li><StatCard icon={Users} label="New applications" value="19" delta="Up 4 on last week" deltaTone="success" /></li>
          <li><StatCard icon={Clock} label="Awaiting your review" value="23" delta="Oldest waiting 4 days" deltaTone="warning" /></li>
          <li><StatCard icon={TrendingUp} label="Median response" value="3.1d" delta="Faster than platform median" deltaTone="success" /></li>
        </ul>

        <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_340px]">
          <div className="space-y-6">
            <Card className="p-6">
              <Tabs defaultValue="live">
                <TabsList aria-label="Job listings">
                  <TabsTrigger value="live">Live ({LIVE_JOBS.length})</TabsTrigger>
                  <TabsTrigger value="drafts">Drafts ({DRAFT_JOBS.length})</TabsTrigger>
                </TabsList>

                <TabsContent value="live">
                  <ul className="divide-y divide-border">
                    {LIVE_JOBS.map((job) => (
                      <li key={job.id} className="flex flex-wrap items-center gap-4 py-3 first:pt-0">
                        <div className="min-w-0 flex-1">
                          <p className="text-small font-medium text-foreground">{job.title}</p>
                          <p className="text-caption text-muted-foreground">
                            {job.applicants} applicants &middot; closes {job.closes}
                          </p>
                        </div>
                        {job.newApplicants > 0 ? (
                          <Badge tone="info" showDot>
                            {job.newApplicants} new
                          </Badge>
                        ) : null}
                        <Button variant="outline" size="sm">
                          Review
                        </Button>
                      </li>
                    ))}
                  </ul>
                </TabsContent>

                <TabsContent value="drafts">
                  <ul className="divide-y divide-border">
                    {DRAFT_JOBS.map((job) => (
                      <li key={job.id} className="flex flex-wrap items-center gap-4 py-3 first:pt-0">
                        <div className="min-w-0 flex-1">
                          <p className="text-small font-medium text-foreground">{job.title}</p>
                          <p className="text-caption text-muted-foreground">{job.updated}</p>
                        </div>
                        <Badge tone="warning" showDot>
                          Needs {job.missing}
                        </Badge>
                        <Button variant="outline" size="sm">
                          Continue
                        </Button>
                      </li>
                    ))}
                  </ul>
                </TabsContent>
              </Tabs>
            </Card>

            <Card className="space-y-4 p-6">
              <div className="flex items-center justify-between gap-3">
                <h2 className="text-h4 text-foreground">Candidate pipeline</h2>
                <Button variant="link" size="sm" className="h-auto p-0" asChild>
                  <Link href="/recruiter/candidates">See all candidates</Link>
                </Button>
              </div>
              <ul className="divide-y divide-border">
                {PIPELINE.map((candidate) => (
                  <li key={candidate.name} className="flex flex-wrap items-center gap-4 py-3 first:pt-0">
                    <Avatar name={candidate.name} size="sm" />
                    <div className="min-w-0 flex-1">
                      <p className="text-small font-medium text-foreground">{candidate.name}</p>
                      <p className="truncate text-caption text-muted-foreground">{candidate.role}</p>
                    </div>
                    <StatusBadge status={candidate.status} />
                    <span className="w-20 text-right text-caption text-muted-foreground">
                      {candidate.when}
                    </span>
                  </li>
                ))}
              </ul>
            </Card>
          </div>

          <div className="space-y-6">
            <Card className="space-y-3 p-6">
              <h2 className="text-h4 text-foreground">Company profile</h2>
              <dl className="space-y-2.5 text-small">
                <div className="flex justify-between gap-3">
                  <dt className="text-muted-foreground">Verification</dt>
                  <dd className="font-medium text-foreground">Valid to Aug 2027</dd>
                </div>
                <div className="flex justify-between gap-3">
                  <dt className="text-muted-foreground">Profile views</dt>
                  <dd className="font-medium text-foreground">1,204 this month</dd>
                </div>
                <div className="flex justify-between gap-3">
                  <dt className="text-muted-foreground">Saved by</dt>
                  <dd className="font-medium text-foreground">318 candidates</dd>
                </div>
              </dl>
              <Button variant="outline" className="w-full">
                Edit company profile
              </Button>
            </Card>

            <Card className="space-y-4 p-6">
              <h2 className="text-h4 text-foreground">Upcoming interviews</h2>
              <ul className="space-y-3">
                {[
                  ["Priya Sharma", "Junior Data Analyst", "Today · 15:00 · Video"],
                  ["Tom Nguyen", "Graduate Software Engineer", "Thu 17 Sep · 11:00 · On-site"],
                ].map(([name, role, when]) => (
                  <li key={name} className="rounded-md bg-muted p-4">
                    <p className="text-small font-medium text-foreground">{name}</p>
                    <p className="text-caption text-muted-foreground">{role}</p>
                    <p className="mt-1.5 flex items-center gap-1.5 text-caption text-primary">
                      <Clock className="size-3.5" aria-hidden="true" />
                      {when}
                    </p>
                  </li>
                ))}
              </ul>
            </Card>

            <Card className="space-y-3 p-6">
              <h2 className="text-h4 text-foreground">Recent activity</h2>
              <ul className="space-y-2.5">
                {ACTIVITY.map((item) => (
                  <li key={item} className="text-small text-muted-foreground">
                    {item}
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
