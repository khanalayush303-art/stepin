import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { PageHeader } from "@/components/layout/page-header";
import { AidxErrorState, Chips } from "@/components/aidx/aidx-ui";
import { Alert } from "@/components/ui/alert";
import { AidxUnavailableError, stepInPublicGet } from "@/lib/aidx/api";
import { formatDate, labels } from "@/lib/aidx/format";
import type { PublicJob } from "@/lib/aidx/types";

type Props = { params: Promise<{ id: string }> };

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/**
 * Reads the StepIn job and only accepts Research jobs. A Career posting must never be shown under
 * AIDX, so anything else resolves to null and becomes a not-found page.
 */
async function loadOpportunity(id: string): Promise<PublicJob | null> {
  if (!UUID.test(id)) return null;
  const job = await stepInPublicGet<PublicJob>(`/jobs/${encodeURIComponent(id)}`);
  return job && job.category === "Research" ? job : null;
}

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const { id } = await params;
  try {
    const job = await loadOpportunity(id);
    if (!job) return { title: "Opportunity not found" };
    const description = `${job.title} at ${job.companyName}, ${job.location}. Research opportunity with AIDX Lab.`;
    return {
      title: job.title,
      description,
      alternates: { canonical: `/aidx/opportunities/${job.id}` },
      openGraph: { title: job.title, description, type: "article", url: `/aidx/opportunities/${job.id}` },
    };
  } catch {
    return { title: "Opportunity" };
  }
}

export default async function OpportunityPage({ params }: Props) {
  const { id } = await params;

  let job: PublicJob | null;
  try {
    job = await loadOpportunity(id);
  } catch (error) {
    if (error instanceof AidxUnavailableError) {
      return (
        <div className="container-page py-12">
          <AidxErrorState what="this opportunity" />
        </div>
      );
    }
    throw error;
  }

  if (!job) notFound();

  const posted = formatDate(job.publishedAt);
  const paragraphs = job.description
    .split(/\n\s*\n/)
    .map((block) => block.trim())
    .filter(Boolean);
  const skills = job.skills ?? [];

  return (
    <>
      <PageHeader
        crumbs={[
          { label: "AIDX Lab", href: "/aidx" },
          { label: "Opportunities", href: "/aidx/opportunities" },
          { label: job.title },
        ]}
        title={job.title}
        description={`${job.companyName} · ${job.location}`}
      >
        <p className="text-small text-muted-foreground">Research opportunity{posted ? ` · Posted ${posted}` : ""}</p>
      </PageHeader>

      <div className="container-page grid gap-12 py-12 lg:grid-cols-[2fr_1fr]">
        <article className="space-y-5">
          <section aria-labelledby="about-role-heading" className="space-y-4">
            <h2 id="about-role-heading" className="text-h4 text-foreground">
              About this role
            </h2>
            {paragraphs.length ? (
              paragraphs.map((block, index) => (
                <p key={index} className="text-body leading-relaxed text-foreground-secondary">
                  {block}
                </p>
              ))
            ) : (
              <p className="text-body text-muted-foreground">The full description has not been added yet.</p>
            )}
          </section>

          {skills.length ? (
            <section aria-labelledby="skills-heading" className="space-y-3">
              <h2 id="skills-heading" className="text-h4 text-foreground">
                Skills
              </h2>
              <Chips items={skills} />
            </section>
          ) : null}
        </article>

        <aside aria-labelledby="apply-heading" className="space-y-8">
          <section className="space-y-4 rounded-lg border border-border bg-surface p-6">
            <h2 id="apply-heading" className="text-h4 text-foreground">
              Apply
            </h2>
            <p className="text-small text-foreground-secondary">
              Applications are handled on StepIn. You need a StepIn candidate account to apply. If you are not signed in,
              you will be asked to sign in first.
            </p>
            <Link
              href={`/dashboard/discover/${job.id}`}
              className="inline-flex w-full items-center justify-center rounded-md bg-primary px-4 py-2 text-small font-semibold text-primary-foreground hover:opacity-90 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
            >
              View and apply on StepIn
            </Link>
          </section>

          <section aria-labelledby="details-heading" className="space-y-3">
            <h2 id="details-heading" className="text-h4 text-foreground">
              Details
            </h2>
            <dl className="space-y-3 text-small">
              <div>
                <dt className="font-medium text-foreground">Organisation</dt>
                <dd className="text-foreground-secondary">{job.companyName}</dd>
              </div>
              <div>
                <dt className="font-medium text-foreground">Location</dt>
                <dd className="text-foreground-secondary">{job.location}</dd>
              </div>
              <div>
                <dt className="font-medium text-foreground">Employment</dt>
                <dd className="text-foreground-secondary">{labels.employment(job.employmentType)}</dd>
              </div>
              <div>
                <dt className="font-medium text-foreground">Workplace</dt>
                <dd className="text-foreground-secondary">{labels.workplace(job.workplaceType)}</dd>
              </div>
              {job.compensation ? (
                <div>
                  <dt className="font-medium text-foreground">Compensation</dt>
                  <dd className="text-foreground-secondary">{job.compensation}</dd>
                </div>
              ) : null}
            </dl>
          </section>

          {job.aidxProjectSlug && job.aidxProjectTitle ? (
            <section aria-labelledby="project-heading" className="space-y-3">
              <h2 id="project-heading" className="text-h4 text-foreground">
                Research context
              </h2>
              <p className="text-small text-foreground-secondary">
                This role is linked to the project{" "}
                <Link
                  href={`/aidx/projects/${job.aidxProjectSlug}`}
                  className="font-medium text-primary underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
                >
                  {job.aidxProjectTitle}
                </Link>
                .
              </p>
            </section>
          ) : null}

          <Alert tone="info" title="Looking for something else?">
            <Link href="/aidx/opportunities" className="font-medium text-primary underline-offset-4 hover:underline">
              Back to all research opportunities
            </Link>
          </Alert>
        </aside>
      </div>
    </>
  );
}
