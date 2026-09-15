import Link from "next/link";
import { Bookmark, Briefcase, Building2, Clock, MapPin } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { VerifiedBadge } from "./verified-badge";
import { daysUntil, formatDate } from "@/lib/utils";
import type { Job } from "@/lib/types";

/**
 * JobCard — mirrors the Figma "Job Card" component.
 * The title is the single link for the whole card; the save control is the only
 * other interactive element, so the card never nests a link inside a link.
 */
export function JobCard({ job }: { job: Job }) {
  const closingIn = daysUntil(job.closesAt);
  const closingSoon = closingIn <= 7;

  return (
    <Card interactive className="flex flex-col gap-4 p-6">
      <div className="flex items-start gap-3">
        <span
          className="flex size-12 shrink-0 items-center justify-center rounded-md bg-primary-subtle text-primary"
          aria-hidden="true"
        >
          <Building2 className="size-6" />
        </span>

        <div className="flex min-w-0 flex-1 flex-col gap-1">
          <h3 className="text-h4 text-foreground">
            <Link
              href={`/jobs/${job.slug}`}
              className="rounded-sm after:absolute after:inset-0 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
            >
              {job.title}
            </Link>
          </h3>
          <div className="flex flex-wrap items-center gap-2">
            <span className="text-small font-medium text-foreground-secondary">
              {job.company.name}
            </span>
            <VerifiedBadge verified={job.company.verified} />
          </div>
        </div>

        <Button
          variant="ghost"
          size="icon"
          aria-label={`Save ${job.title} at ${job.company.name}`}
          className="relative z-10 -mr-2 -mt-2 text-muted-foreground"
        >
          <Bookmark />
        </Button>
      </div>

      <ul className="flex flex-wrap items-center gap-x-4 gap-y-2 text-small text-muted-foreground">
        <li className="flex items-center gap-1.5">
          <MapPin className="size-4 shrink-0" aria-hidden="true" />
          {job.location} &middot; {job.workMode}
        </li>
        <li className="flex items-center gap-1.5">
          <Briefcase className="size-4 shrink-0" aria-hidden="true" />
          {job.experience}
        </li>
        <li className="flex items-center gap-1.5">
          <Clock className="size-4 shrink-0" aria-hidden="true" />
          {closingIn === 0 ? "Closes today" : `Closes in ${closingIn} days`}
          {closingSoon ? (
            <Badge tone="warning" showDot className="ml-1">
              Closing soon
            </Badge>
          ) : null}
        </li>
      </ul>

      <p className="text-small text-foreground-secondary">{job.summary}</p>

      <ul className="flex flex-wrap gap-2">
        {[...job.skills, job.workType].map((tag) => (
          <li
            key={tag}
            className="rounded-sm bg-muted px-2 py-1 text-caption text-foreground-secondary"
          >
            {tag}
          </li>
        ))}
      </ul>

      {job.isInternship && job.internship ? (
        <dl className="grid grid-cols-2 gap-x-4 gap-y-2 rounded-md bg-accent-subtle p-3 text-caption text-accent-subtle-fg sm:grid-cols-4">
          <div>
            <dt>Duration</dt>
            <dd className="font-medium">{job.internship.durationWeeks} weeks</dd>
          </div>
          <div>
            <dt>Paid</dt>
            <dd className="font-medium">{job.internship.paid ? "Yes" : "Unpaid"}</dd>
          </div>
          <div>
            <dt>Cohort starts</dt>
            <dd className="font-medium">{formatDate(job.internship.cohortStart)}</dd>
          </div>
          <div>
            <dt>Converts to grad role</dt>
            <dd className="font-medium">
              {job.internship.convertsToGraduateRole ? "Possible" : "No"}
            </dd>
          </div>
        </dl>
      ) : null}

      <div className="mt-auto flex items-center justify-between gap-3 border-t border-border pt-4">
        <div>
          <p className="text-small font-medium text-foreground">{job.compensation}</p>
          <p className="text-caption text-muted-foreground">
            Posted {formatDate(job.postedAt)}
          </p>
        </div>
        <Button size="sm" asChild className="relative z-10">
          <Link href={`/jobs/${job.slug}`} tabIndex={-1}>
            View role
          </Link>
        </Button>
      </div>
    </Card>
  );
}
