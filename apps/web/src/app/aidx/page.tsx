import type { Metadata } from "next";
import Link from "next/link";
import { LinkCard, AidxErrorState, AidxEmptyState, AIDX_ICONS, SectionHeading, Chips } from "@/components/aidx/aidx-ui";
import { Button } from "@/components/ui/button";
import { trySection, type PageResult } from "@/lib/aidx/api";
import { formatDate, labels } from "@/lib/aidx/format";
import type { AidxEvent, NewsSummary, Opportunity, ProjectSummary, Publication, ResearchArea } from "@/lib/aidx/types";

export const metadata: Metadata = {
  title: { absolute: "AIDX Lab — Research, projects and collaboration" },
  description: "AIDX Lab's research areas, projects, people, publications, news, events and research opportunities.",
  alternates: { canonical: "/aidx" },
  openGraph: { title: "AIDX Lab", description: "Research, projects and collaboration at AIDX Lab.", url: "/aidx" },
};

/**
 * Landing page. Every section fetches on its own, and an empty or unavailable section shows
 * an honest state rather than placeholder records. Nothing here is fabricated.
 */
export default async function AidxHomePage() {
  const [areas, projects, opportunities, news, events, publications] = await Promise.all([
    trySection<ResearchArea[]>("/research"),
    trySection<PageResult<ProjectSummary>>("/projects?featured=true&pageSize=3"),
    trySection<PageResult<Opportunity>>("/opportunities?pageSize=3"),
    trySection<PageResult<NewsSummary>>("/news?pageSize=3"),
    trySection<PageResult<AidxEvent>>("/events?upcoming=true&pageSize=3"),
    trySection<PageResult<Publication>>("/publications?pageSize=3"),
  ]);

  return (
    <>
      <section className="border-b border-border bg-surface" aria-labelledby="hero-title">
        <div className="container-page grid gap-8 py-16 lg:grid-cols-[1.4fr_1fr] lg:items-end">
          <div className="space-y-6">
            <p className="text-caption font-semibold uppercase tracking-wide text-primary">AIDX Lab · part of StepIn</p>
            <h1 id="hero-title" className="text-h1 text-foreground">
              Advancing AI and data intelligence through research and collaboration.
            </h1>
            <p className="max-w-2xl text-body-lg text-muted-foreground">
              AIDX Lab brings together research projects, researchers, publications and opportunities in artificial
              intelligence and data science.
            </p>
            <div className="flex flex-wrap gap-3">
              <Button asChild>
                <Link href="/aidx/projects">Explore projects</Link>
              </Button>
              <Button asChild variant="outline">
                <Link href="/aidx/opportunities">Research opportunities</Link>
              </Button>
            </div>
          </div>
          <nav aria-label="Lab sections" className="grid grid-cols-2 gap-3 text-small">
            {[
              ["/aidx/research", "Research areas"],
              ["/aidx/people", "People"],
              ["/aidx/publications", "Publications"],
              ["/aidx/events", "Events"],
            ].map(([href, label]) => (
              <Link
                key={href}
                href={href}
                className="rounded-lg border border-border bg-background p-4 font-medium text-foreground hover:border-primary/60 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
              >
                {label}
              </Link>
            ))}
          </nav>
        </div>
      </section>

      <div className="container-page space-y-16 py-14">
        <section aria-labelledby="areas-heading" className="space-y-6">
          <SectionHeading id="areas-heading" title="Research areas" href="/aidx/research" linkLabel="All research areas" />
          {areas.failed ? (
            <AidxErrorState what="research areas" />
          ) : !areas.data?.length ? (
            <AidxEmptyState title="No research areas published yet" description="Research areas appear here once the lab publishes them." />
          ) : (
            <ul className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
              {areas.data.slice(0, 6).map((area) => (
                <li key={area.id}>
                  <LinkCard href={`/aidx/research/${area.slug}`} title={area.name} description={area.description} icon={AIDX_ICONS.research} />
                </li>
              ))}
            </ul>
          )}
        </section>

        <section aria-labelledby="projects-heading" className="space-y-6">
          <SectionHeading id="projects-heading" title="Featured projects" href="/aidx/projects" linkLabel="All projects" />
          {projects.failed ? (
            <AidxErrorState what="featured projects" />
          ) : !projects.data?.items.length ? (
            <AidxEmptyState title="No featured projects yet" description="Featured projects will be shown here once published." />
          ) : (
            <ul className="grid gap-4 md:grid-cols-3">
              {projects.data.items.map((project) => (
                <li key={project.id}>
                  <LinkCard
                    href={`/aidx/projects/${project.slug}`}
                    title={project.title}
                    description={project.shortDescription}
                    icon={AIDX_ICONS.projects}
                  >
                    <Chips items={project.researchAreas} />
                  </LinkCard>
                </li>
              ))}
            </ul>
          )}
        </section>

        <section aria-labelledby="opportunities-heading" className="space-y-6">
          <SectionHeading
            id="opportunities-heading"
            title="Research opportunities"
            description="Open roles on StepIn's job platform. Sign in to view a role and apply."
            href="/aidx/opportunities"
            linkLabel="All opportunities"
          />
          {opportunities.failed ? (
            <AidxErrorState what="research opportunities" />
          ) : !opportunities.data?.items.length ? (
            <AidxEmptyState title="No research opportunities right now" description="New research roles are listed here as they are published." />
          ) : (
            <ul className="grid gap-4 md:grid-cols-3">
              {opportunities.data.items.map((job) => (
                <li key={job.id}>
                  <LinkCard
                    href={`/dashboard/discover/${job.id}`}
                    title={job.title}
                    description={`${job.companyName} · ${job.location}`}
                    icon={AIDX_ICONS.opportunities}
                  >
                    <p className="text-small text-muted-foreground">{labels.employment(job.employmentType)}</p>
                  </LinkCard>
                </li>
              ))}
            </ul>
          )}
        </section>

        <div className="grid gap-12 lg:grid-cols-2">
          <section aria-labelledby="news-heading" className="space-y-6">
            <SectionHeading id="news-heading" title="Latest news" href="/aidx/news" linkLabel="All news" />
            {news.failed ? (
              <AidxErrorState what="news" />
            ) : !news.data?.items.length ? (
              <AidxEmptyState title="No news yet" description="Lab news will appear here." />
            ) : (
              <ul className="space-y-4">
                {news.data.items.map((item) => (
                  <li key={item.id}>
                    <LinkCard href={`/aidx/news/${item.slug}`} title={item.title} description={item.summary} meta={formatDate(item.publishedAt)} />
                  </li>
                ))}
              </ul>
            )}
          </section>

          <section aria-labelledby="events-heading" className="space-y-6">
            <SectionHeading id="events-heading" title="Upcoming events" href="/aidx/events" linkLabel="All events" />
            {events.failed ? (
              <AidxErrorState what="events" />
            ) : !events.data?.items.length ? (
              <AidxEmptyState title="No upcoming events" description="Talks and workshops will be listed here." />
            ) : (
              <ul className="space-y-4">
                {events.data.items.map((event) => (
                  <li key={event.id}>
                    <LinkCard href={`/aidx/events/${event.slug}`} title={event.title} description={event.location ?? "Location to be confirmed"} meta={formatDate(event.startsAt)} />
                  </li>
                ))}
              </ul>
            )}
          </section>
        </div>

        <section aria-labelledby="publications-heading" className="space-y-6">
          <SectionHeading id="publications-heading" title="Selected publications" href="/aidx/publications" linkLabel="All publications" />
          {publications.failed ? (
            <AidxErrorState what="publications" />
          ) : !publications.data?.items.length ? (
            <AidxEmptyState title="No publications listed yet" description="Publications from the lab appear here." />
          ) : (
            <ul className="grid gap-4 md:grid-cols-3">
              {publications.data.items.map((pub) => (
                <li key={pub.id}>
                  <LinkCard href="/aidx/publications" title={pub.title} description={pub.authors.join(", ")} meta={`${pub.year} · ${labels.publication(pub.publicationType)}`} icon={AIDX_ICONS.publications} />
                </li>
              ))}
            </ul>
          )}
        </section>

        <section aria-labelledby="cta-heading" className="rounded-xl border border-border bg-surface p-8 sm:p-12">
          <h2 id="cta-heading" className="text-h2 text-foreground">
            Work with the lab
          </h2>
          <p className="mt-3 max-w-2xl text-body text-muted-foreground">
            Learn how AIDX Lab fits within StepIn, or get in touch about research collaboration.
          </p>
          <div className="mt-6 flex flex-wrap gap-3">
            <Button asChild>
              <Link href="/aidx/about">About AIDX Lab</Link>
            </Button>
            <Button asChild variant="outline">
              <Link href="/aidx/contact">Contact</Link>
            </Button>
          </div>
        </section>
      </div>
    </>
  );
}
