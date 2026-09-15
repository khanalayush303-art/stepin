import type { Metadata } from "next";
import { CalendarClock, DollarSign, GraduationCap } from "lucide-react";
import { PageHeader } from "@/components/layout/page-header";
import { JobSearchRow } from "@/components/jobs/job-search-row";
import { JobsBrowser, type BrowseState } from "@/components/jobs/jobs-browser";
import { Card } from "@/components/ui/card";
import { buildJobFilters } from "@/lib/filters";
import { JOBS } from "@/lib/placeholder-data";

export const metadata: Metadata = {
  title: "Internships & placements",
  description:
    "Paid internships and university placements from verified employers, with cohort dates and duration shown up front.",
};

const STATES: BrowseState[] = ["ready", "loading", "empty", "error"];

/** Internship-specific context — same design system, different information. */
const INTERNSHIP_FACTS = [
  {
    icon: CalendarClock,
    title: "Cohort dates are published",
    body: "Start date and duration appear on every listing, so you can check it against your semester before you apply.",
  },
  {
    icon: DollarSign,
    title: "Paid status is never hidden",
    body: "Each placement states its hourly rate, or is labelled unpaid. No listing can leave it blank.",
  },
  {
    icon: GraduationCap,
    title: "Counts toward WIL where offered",
    body: "Placements that can be credited as work-integrated learning are marked, with the employer's contact for your faculty.",
  },
];

export default async function InternshipsPage({
  searchParams,
}: {
  searchParams: Promise<{ state?: string }>;
}) {
  const { state } = await searchParams;
  const internships = JOBS.filter((job) => job.isInternship);
  const view = STATES.includes(state as BrowseState) ? (state as BrowseState) : "ready";

  return (
    <>
      <PageHeader
        crumbs={[{ label: "Home", href: "/" }, { label: "Internships" }]}
        title="Internships & placements"
        description="Paid placements and work-integrated learning, with duration, cohort start and pay stated on every listing."
      >
        <JobSearchRow keywordPlaceholder="Field, skill or company" />
      </PageHeader>

      <section className="container-page pt-10">
        <ul className="grid gap-5 md:grid-cols-3">
          {INTERNSHIP_FACTS.map((fact) => (
            <li key={fact.title}>
              <Card className="flex h-full gap-3 p-5">
                <span
                  className="flex size-10 shrink-0 items-center justify-center rounded-md bg-accent-subtle text-accent-subtle-fg"
                  aria-hidden="true"
                >
                  <fact.icon className="size-5" />
                </span>
                <div className="space-y-1">
                  <p className="text-small font-medium text-foreground">{fact.title}</p>
                  <p className="text-caption text-muted-foreground">{fact.body}</p>
                </div>
              </Card>
            </li>
          ))}
        </ul>
      </section>

      <JobsBrowser
        jobs={internships}
        groups={buildJobFilters(internships)}
        state={view}
        countNoun="placements"
        emptyCopy={{
          title: "No placements match those filters",
          description:
            "Internship listings are seasonal. Try clearing the location filter, or check back when the summer cohort opens in October.",
        }}
      />
    </>
  );
}
