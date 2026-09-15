import Link from "next/link";
import {
  ArrowRight,
  BadgeCheck,
  Briefcase,
  Check,
  Clock,
  FileText,
  GraduationCap,
  MapPin,
  Search,
  TrendingUp,
  User,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { JobCard } from "@/components/jobs/job-card";
import { JOBS, JOURNEY, PLATFORM_STATS } from "@/lib/placeholder-data";

const POPULAR = [
  "Data analyst",
  "Software engineering intern",
  "Nursing graduate",
  "Cyber security",
  "Accounting",
];

const JOURNEY_ICONS = [Search, TrendingUp, GraduationCap, FileText, Clock, User, BadgeCheck];

const APPLICANT_VALUE = [
  "One profile you build once and reuse",
  "See the stage every application is at",
  "Prep prompts written for the specific role",
  "Only employers we have verified",
];

const EMPLOYER_VALUE = [
  "Reach candidates from a named institution",
  "Structured applications, not inbox chaos",
  "Move candidates through stages in one view",
  "Verification badge that candidates trust",
];

export default function HomePage() {
  const featured = JOBS.slice(0, 3);

  return (
    <>
      {/* ---------------------------------------------------------------- Hero */}
      <section className="border-b border-border bg-surface">
        <div className="container-page flex flex-col items-center gap-7 py-16 text-center lg:py-24">
          <p className="inline-flex items-center gap-2 rounded-full bg-accent-subtle px-3 py-1.5 text-caption text-accent-subtle-fg">
            <BadgeCheck className="size-3.5" aria-hidden="true" />
            Every employer on StepIn is verified before they can post
          </p>

          <h1 className="max-w-4xl text-h1 text-foreground lg:text-display">
            Find opportunities. Apply with confidence.
          </h1>

          <p className="max-w-2xl text-body text-muted-foreground lg:text-body-lg">
            Graduate roles and internships from employers we have checked, with every stage
            of your application visible in one place.
          </p>

          {/* Search — stacks to full-width fields below md rather than shrinking. */}
          <form
            role="search"
            onSubmit={undefined}
            className="w-full max-w-4xl rounded-xl border border-border bg-surface p-2 shadow-md md:rounded-full"
          >
            <div className="flex flex-col gap-2 md:flex-row md:items-center">
              <div className="relative flex-1">
                <label htmlFor="hero-q" className="sr-only">
                  What role are you looking for?
                </label>
                <Search
                  className="pointer-events-none absolute left-4 top-1/2 size-[18px] -translate-y-1/2 text-muted-foreground"
                  aria-hidden="true"
                />
                <Input
                  id="hero-q"
                  name="q"
                  placeholder="Job title, skill or company"
                  className="border-transparent bg-transparent pl-11 text-left"
                />
              </div>

              <span className="hidden h-8 w-px shrink-0 bg-border md:block" aria-hidden="true" />

              <div className="relative md:w-56">
                <label htmlFor="hero-where" className="sr-only">
                  Where?
                </label>
                <MapPin
                  className="pointer-events-none absolute left-4 top-1/2 size-[18px] -translate-y-1/2 text-muted-foreground"
                  aria-hidden="true"
                />
                <Input
                  id="hero-where"
                  name="location"
                  placeholder="Sydney, NSW"
                  className="border-transparent bg-transparent pl-11"
                />
              </div>

              <span className="hidden h-8 w-px shrink-0 bg-border md:block" aria-hidden="true" />

              <div className="relative md:w-48">
                <label htmlFor="hero-type" className="sr-only">
                  Work type
                </label>
                <Briefcase
                  className="pointer-events-none absolute left-4 top-1/2 size-[18px] -translate-y-1/2 text-muted-foreground"
                  aria-hidden="true"
                />
                <Input
                  id="hero-type"
                  name="type"
                  placeholder="Any work type"
                  className="border-transparent bg-transparent pl-11"
                />
              </div>

              <Button type="submit" size="lg" className="md:rounded-full md:px-8">
                Search
              </Button>
            </div>
          </form>

          <div className="flex flex-wrap items-center justify-center gap-2">
            <span className="text-caption text-muted-foreground">Popular:</span>
            {POPULAR.map((term) => (
              <Link
                key={term}
                href={`/jobs?q=${encodeURIComponent(term)}`}
                className="rounded-full bg-muted px-3 py-1 text-caption text-foreground-secondary hover:bg-secondary-hover focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
              >
                {term}
              </Link>
            ))}
          </div>
        </div>
      </section>

      {/* -------------------------------------------------------- Trust strip */}
      <section aria-label="Platform at a glance" className="border-b border-border bg-surface">
        <div className="container-page grid grid-cols-2 gap-8 py-8 lg:grid-cols-4">
          {PLATFORM_STATS.map((stat) => (
            <div key={stat.label} className="flex flex-col items-center gap-1 text-center">
              <p className="text-h2 text-primary">{stat.value}</p>
              <p className="text-small text-muted-foreground">{stat.label}</p>
            </div>
          ))}
        </div>
      </section>

      {/* ------------------------------------------------ Featured opportunities */}
      <section className="container-page py-16 lg:py-24">
        <div className="flex flex-wrap items-end justify-between gap-4">
          <div className="space-y-1.5">
            <h2 className="text-h2 text-foreground">Featured opportunities</h2>
            <p className="text-body text-muted-foreground">
              Roles from verified employers, refreshed every morning.
            </p>
          </div>
          <Link
            href="/jobs"
            className="inline-flex items-center gap-2 rounded-sm text-small font-semibold text-primary hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
          >
            Browse all roles
            <ArrowRight className="size-4" aria-hidden="true" />
          </Link>
        </div>

        <ul className="mt-10 grid gap-6 md:grid-cols-2 xl:grid-cols-3">
          {featured.map((job) => (
            <li key={job.id} className="relative flex">
              <JobCard job={job} />
            </li>
          ))}
        </ul>
      </section>

      {/* ---------------------------------------------------- How it works */}
      <section className="border-y border-border bg-surface py-16 lg:py-24">
        <div className="container-page">
          <div className="flex flex-col items-center gap-2 text-center">
            <h2 className="text-h2 text-foreground">How it works</h2>
            <p className="max-w-2xl text-body text-muted-foreground lg:text-body-lg">
              Seven stages, all of them visible to you. No black box between applying and
              hearing back.
            </p>
          </div>

          <ol className="mt-10 grid gap-5 sm:grid-cols-2 xl:grid-cols-4">
            {JOURNEY.map((step, i) => {
              const Icon = JOURNEY_ICONS[i];
              return (
                <li key={step.key}>
                  <Card className="flex h-full flex-col gap-3 p-6">
                    <div className="flex items-center gap-3">
                      <span
                        className="flex size-10 shrink-0 items-center justify-center rounded-md bg-primary-subtle text-primary"
                        aria-hidden="true"
                      >
                        <Icon className="size-5" />
                      </span>
                      <div>
                        <p className="text-caption text-muted-foreground">STEP {i + 1}</p>
                        <p className="text-h4 text-foreground">{step.title}</p>
                      </div>
                    </div>
                    <p className="text-small text-muted-foreground">{step.applicant}</p>
                  </Card>
                </li>
              );
            })}
          </ol>
        </div>
      </section>

      {/* --------------------------------------------------------- Value split */}
      <section className="container-page py-16 lg:py-24">
        <div className="grid gap-6 lg:grid-cols-2">
          <div className="rounded-xl bg-primary p-8 lg:p-12">
            <h2 className="text-h3 text-on-brand">For students &amp; graduates</h2>
            <ul className="mt-5 space-y-3">
              {APPLICANT_VALUE.map((item) => (
                <li key={item} className="flex items-start gap-2.5 text-body text-primary-subtle">
                  <Check className="mt-1 size-[18px] shrink-0 text-on-brand" aria-hidden="true" />
                  {item}
                </li>
              ))}
            </ul>
            <Button
              asChild
              className="mt-7 bg-on-brand text-primary hover:bg-on-brand/90"
            >
              <Link href="/register">Create your profile</Link>
            </Button>
          </div>

          <Card className="p-8 lg:p-12">
            <h2 className="text-h3 text-foreground">For employers</h2>
            <ul className="mt-5 space-y-3">
              {EMPLOYER_VALUE.map((item) => (
                <li key={item} className="flex items-start gap-2.5 text-body text-muted-foreground">
                  <Check className="mt-1 size-[18px] shrink-0 text-success" aria-hidden="true" />
                  {item}
                </li>
              ))}
            </ul>
            <Button asChild className="mt-7">
              <Link href="/register">Post a role</Link>
            </Button>
          </Card>
        </div>
      </section>

      {/* --------------------------------------------------------------- CTA */}
      <section className="border-t border-border bg-surface">
        <div className="container-page flex flex-col items-center gap-5 py-16 text-center lg:py-20">
          <h2 className="max-w-2xl text-h2 text-foreground">
            Build your future, one clear step at a time.
          </h2>
          <p className="max-w-xl text-body text-muted-foreground">
            Free for students and graduates. No card required, and you can delete your
            profile at any time.
          </p>
          <div className="flex flex-wrap justify-center gap-3">
            <Button size="lg" asChild>
              <Link href="/register">Create your account</Link>
            </Button>
            <Button size="lg" variant="outline" asChild>
              <Link href="/how-it-works">See how it works</Link>
            </Button>
          </div>
        </div>
      </section>
    </>
  );
}
