import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { PageHeader } from "@/components/layout/page-header";
import { AidxErrorState } from "@/components/aidx/aidx-ui";
import { Badge } from "@/components/ui/badge";
import { aidxGet, AidxUnavailableError } from "@/lib/aidx/api";
import { labels, safeHttpUrl } from "@/lib/aidx/format";
import type { PublicationDetail } from "@/lib/aidx/types";

type Props = { params: Promise<{ id: string }> };

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

async function loadPublication(id: string): Promise<PublicationDetail | null> {
  // A malformed id can never match a publication, so it is a plain not-found without a round trip.
  if (!UUID.test(id)) return null;
  return aidxGet<PublicationDetail>(`/publications/${encodeURIComponent(id)}`);
}

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const { id } = await params;
  try {
    const publication = await loadPublication(id);
    if (!publication) return { title: "Publication not found" };
    const description = publication.abstract
      ? publication.abstract.slice(0, 160)
      : `${publication.title}, ${publication.year}${publication.venue ? `, ${publication.venue}` : ""}.`;
    return {
      title: publication.title,
      description,
      alternates: { canonical: `/aidx/publications/${publication.id}` },
      openGraph: { title: publication.title, description, type: "article", url: `/aidx/publications/${publication.id}` },
    };
  } catch {
    return { title: "Publication" };
  }
}

/** Only the public fields the API returns are shown. Unpublished items are never sent, so none can appear. */
export default async function PublicationPage({ params }: Props) {
  const { id } = await params;

  let publication: PublicationDetail | null;
  try {
    publication = await loadPublication(id);
  } catch (error) {
    if (error instanceof AidxUnavailableError) {
      return (
        <div className="container-page py-12">
          <AidxErrorState what="this publication" />
        </div>
      );
    }
    throw error;
  }

  if (!publication) notFound();

  const externalUrl = safeHttpUrl(publication.externalUrl);
  const doiUrl = publication.doi ? `https://doi.org/${encodeURI(publication.doi)}` : null;
  const abstractBlocks = (publication.abstract ?? "")
    .split(/\n\s*\n/)
    .map((block) => block.trim())
    .filter(Boolean);

  return (
    <>
      <PageHeader
        crumbs={[
          { label: "AIDX Lab", href: "/aidx" },
          { label: "Publications", href: "/aidx/publications" },
          { label: publication.title },
        ]}
        title={publication.title}
        description={[publication.venue, publication.year].filter(Boolean).join(" · ")}
      >
        <Badge tone="neutral" variant="subtle">
          {labels.publication(publication.publicationType)}
        </Badge>
      </PageHeader>

      <div className="container-page grid gap-12 py-12 lg:grid-cols-[2fr_1fr]">
        <article className="space-y-6">
          {publication.authors.length ? (
            <section aria-labelledby="authors-heading" className="space-y-2">
              <h2 id="authors-heading" className="text-h4 text-foreground">
                Authors
              </h2>
              <p className="text-body text-foreground-secondary">{publication.authors.join(", ")}</p>
            </section>
          ) : null}

          <section aria-labelledby="abstract-heading" className="space-y-4">
            <h2 id="abstract-heading" className="text-h4 text-foreground">
              Abstract
            </h2>
            {abstractBlocks.length ? (
              abstractBlocks.map((block, index) => (
                <p key={index} className="text-body leading-relaxed text-foreground-secondary">
                  {block}
                </p>
              ))
            ) : (
              <p className="text-body text-muted-foreground">An abstract has not been added yet.</p>
            )}
          </section>
        </article>

        <aside aria-labelledby="details-heading" className="space-y-8">
          <section className="space-y-3">
            <h2 id="details-heading" className="text-h4 text-foreground">
              Details
            </h2>
            <dl className="space-y-3 text-small">
              {publication.venue ? (
                <div>
                  <dt className="font-medium text-foreground">Venue</dt>
                  <dd className="text-foreground-secondary">{publication.venue}</dd>
                </div>
              ) : null}
              <div>
                <dt className="font-medium text-foreground">Year</dt>
                <dd className="text-foreground-secondary">{publication.year}</dd>
              </div>
              {doiUrl ? (
                <div>
                  <dt className="font-medium text-foreground">DOI</dt>
                  <dd className="break-all">
                    <a
                      href={doiUrl}
                      rel="noopener noreferrer"
                      target="_blank"
                      className="text-primary underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
                    >
                      {publication.doi}
                      <span className="sr-only"> (opens in a new tab)</span>
                    </a>
                  </dd>
                </div>
              ) : null}
            </dl>
            {externalUrl ? (
              <a
                href={externalUrl}
                rel="noopener noreferrer"
                target="_blank"
                className="inline-flex text-small font-medium text-primary underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
              >
                Read the publication
                <span className="sr-only"> (opens in a new tab)</span>
              </a>
            ) : null}
          </section>

          {publication.researchAreas.length ? (
            <section aria-labelledby="areas-heading" className="space-y-3">
              <h2 id="areas-heading" className="text-h4 text-foreground">
                Research areas
              </h2>
              <ul className="space-y-2 text-small">
                {publication.researchAreas.map((area) => (
                  <li key={area.id}>
                    <Link href={`/aidx/research/${area.slug}`} className="font-medium text-primary underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring">
                      {area.name}
                    </Link>
                  </li>
                ))}
              </ul>
            </section>
          ) : null}

          {publication.projects.length ? (
            <section aria-labelledby="projects-heading" className="space-y-3">
              <h2 id="projects-heading" className="text-h4 text-foreground">
                Related projects
              </h2>
              <ul className="space-y-2 text-small">
                {publication.projects.map((project) => (
                  <li key={project.id}>
                    <Link href={`/aidx/projects/${project.slug}`} className="font-medium text-primary underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring">
                      {project.title}
                    </Link>
                  </li>
                ))}
              </ul>
            </section>
          ) : null}

          <Link href="/aidx/publications" className="inline-flex text-small font-medium text-primary underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring">
            Back to all publications
          </Link>
        </aside>
      </div>
    </>
  );
}
