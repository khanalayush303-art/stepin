import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { PageHeader } from "@/components/layout/page-header";
import { AidxErrorState, Chips } from "@/components/aidx/aidx-ui";
import { AidxRichText } from "@/components/aidx/aidx-rich-text";
import { AidxSaveButton } from "@/components/aidx/aidx-save-button";
import { AidxCollaborationForm } from "@/components/aidx/aidx-collaboration-form";
import { Badge } from "@/components/ui/badge";
import { aidxGet, AidxUnavailableError } from "@/lib/aidx/api";
import { formatDate, safeHttpUrl } from "@/lib/aidx/format";
import type { ProjectDetail } from "@/lib/aidx/types";

type Props = { params: Promise<{ slug: string }> };

async function loadProject(slug: string): Promise<ProjectDetail | null> {
  return aidxGet<ProjectDetail>(`/projects/${encodeURIComponent(slug)}`);
}

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const { slug } = await params;
  try {
    const project = await loadProject(slug);
    if (!project) return { title: "Project not found" };
    return {
      title: project.title,
      description: project.shortDescription,
      alternates: { canonical: `/aidx/projects/${project.slug}` },
      openGraph: { title: project.title, description: project.shortDescription, url: `/aidx/projects/${project.slug}` },
    };
  } catch {
    return { title: "Project" };
  }
}

export default async function ProjectPage({ params }: Props) {
  const { slug } = await params;

  let project: ProjectDetail | null;
  try {
    project = await loadProject(slug);
  } catch (error) {
    if (error instanceof AidxUnavailableError) {
      return (
        <div className="container-page py-12">
          <AidxErrorState what="this project" />
        </div>
      );
    }
    throw error;
  }

  if (!project) notFound();

  const dates = [formatDate(project.startDate), formatDate(project.endDate)].filter(Boolean);

  return (
    <>
      <PageHeader
        crumbs={[{ label: "AIDX Lab", href: "/aidx" }, { label: "Projects", href: "/aidx/projects" }, { label: project.title }]}
        title={project.title}
        description={project.shortDescription}
      >
        <div className="flex flex-wrap items-center gap-3">
          {project.featured ? (
            <Badge tone="brand" variant="solid">
              Featured
            </Badge>
          ) : null}
          {dates.length ? <span className="text-small text-muted-foreground">{dates.join(" to ")}</span> : null}
          <AidxSaveButton contentType="project" contentId={project.id} />
        </div>
      </PageHeader>

      <div className="container-page grid gap-12 py-12 lg:grid-cols-[2fr_1fr]">
        <article className="space-y-6">
          <AidxRichText text={project.description} className="space-y-4" />

          {project.milestones.length ? (
            <section aria-labelledby="milestones-heading" className="space-y-3">
              <h2 id="milestones-heading" className="text-h4 text-foreground">
                Milestones
              </h2>
              <ul className="space-y-3">
                {project.milestones.map((milestone, index) => (
                  <li key={index} className="rounded-md border border-border p-3">
                    <div className="flex flex-wrap items-center justify-between gap-2">
                      <p className="text-small font-medium text-foreground">{milestone.title}</p>
                      {milestone.completedDate ? (
                        <Badge tone="success" variant="subtle">
                          Completed {formatDate(milestone.completedDate)}
                        </Badge>
                      ) : milestone.targetDate ? (
                        <Badge tone="neutral" variant="subtle">
                          Target {formatDate(milestone.targetDate)}
                        </Badge>
                      ) : null}
                    </div>
                    {milestone.description ? (
                      <AidxRichText text={milestone.description} className="mt-1 text-small text-muted-foreground" />
                    ) : null}
                  </li>
                ))}
              </ul>
            </section>
          ) : null}

          {project.outcomes ? (
            <section aria-labelledby="outcomes-heading" className="space-y-2">
              <h2 id="outcomes-heading" className="text-h4 text-foreground">
                Outcomes
              </h2>
              <AidxRichText text={project.outcomes} className="space-y-4" />
            </section>
          ) : null}

          {safeHttpUrl(project.externalUrl) ? (
            <p>
              <a
                href={safeHttpUrl(project.externalUrl) ?? undefined}
                rel="noopener noreferrer"
                target="_blank"
                className="font-semibold text-primary underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
              >
                Visit the project page
                <span className="sr-only"> (opens in a new tab)</span>
              </a>
            </p>
          ) : null}
        </article>

        <aside aria-labelledby="project-details" className="space-y-8">
          <section className="space-y-3">
            <h2 id="project-details" className="text-h4 text-foreground">
              Details
            </h2>
            <dl className="space-y-3 text-small">
              {project.researchAreas.length ? (
                <div>
                  <dt className="font-medium text-foreground">Research areas</dt>
                  <dd className="mt-1">
                    <Chips items={project.researchAreas} />
                  </dd>
                </div>
              ) : null}
              {project.technologies.length ? (
                <div>
                  <dt className="font-medium text-foreground">Technologies</dt>
                  <dd className="mt-1">
                    <Chips items={project.technologies} />
                  </dd>
                </div>
              ) : null}
            </dl>
          </section>

          <section className="space-y-3">
            <h2 className="text-h4 text-foreground">Team</h2>
            {project.researchers.length ? (
              <ul className="space-y-2">
                {project.researchers.map((person) => (
                  <li key={person.id} className="text-small">
                    <Link
                      href={`/aidx/people/${person.slug}`}
                      className="font-medium text-primary underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
                    >
                      {person.displayName}
                    </Link>
                    {person.role ? <span className="text-muted-foreground"> · {person.role}</span> : null}
                  </li>
                ))}
              </ul>
            ) : (
              <p className="text-small text-muted-foreground">Team members for this project will be listed here.</p>
            )}
          </section>

          <section aria-labelledby="project-enquiry" className="space-y-3 rounded-lg border border-border bg-surface p-5">
            <h2 id="project-enquiry" className="text-h4 text-foreground">
              Interested in this project?
            </h2>
            <p className="text-small text-muted-foreground">Send a message to the team.</p>
            <AidxCollaborationForm projectId={project.id} />
          </section>
        </aside>
      </div>
    </>
  );
}
