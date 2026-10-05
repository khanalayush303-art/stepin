import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { PageHeader } from "@/components/layout/page-header";
import { AidxErrorState } from "@/components/aidx/aidx-ui";
import { aidxGet, AidxUnavailableError } from "@/lib/aidx/api";
import { labels } from "@/lib/aidx/format";
import type { Person } from "@/lib/aidx/types";

type Props = { params: Promise<{ slug: string }> };

async function loadPerson(slug: string): Promise<Person | null> {
  return aidxGet<Person>(`/people/${encodeURIComponent(slug)}`);
}

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const { slug } = await params;
  try {
    const person = await loadPerson(slug);
    if (!person) return { title: "Profile not found" };
    return {
      title: person.displayName,
      description: person.position ?? `${person.displayName}, ${labels.researcher(person.category)} at AIDX Lab.`,
      alternates: { canonical: `/aidx/people/${person.slug}` },
    };
  } catch {
    return { title: "Profile" };
  }
}

/**
 * Only the public fields the API returns are shown. The API never sends email addresses,
 * account identifiers or storage keys, so none can appear here.
 */
export default async function PersonPage({ params }: Props) {
  const { slug } = await params;

  let person: Person | null;
  try {
    person = await loadPerson(slug);
  } catch (error) {
    if (error instanceof AidxUnavailableError) {
      return (
        <div className="container-page py-12">
          <AidxErrorState what="this profile" />
        </div>
      );
    }
    throw error;
  }

  if (!person) notFound();

  const links = [
    { href: person.websiteUrl, label: "Website" },
    { href: person.orcidUrl, label: "ORCID" },
    { href: person.googleScholarUrl, label: "Google Scholar" },
    { href: person.linkedInUrl, label: "LinkedIn" },
  ].filter((link): link is { href: string; label: string } => Boolean(link.href));

  return (
    <>
      <PageHeader
        crumbs={[{ label: "AIDX Lab", href: "/aidx" }, { label: "People", href: "/aidx/people" }, { label: person.displayName }]}
        title={person.displayName}
        description={[person.position, labels.researcher(person.category)].filter(Boolean).join(" · ")}
      />

      <div className="container-page grid gap-12 py-12 lg:grid-cols-[2fr_1fr]">
        <article className="space-y-5">
          {person.biography ? (
            person.biography
              .split(/\n\s*\n/)
              .map((block) => block.trim())
              .filter(Boolean)
              .map((block, index) => (
                <p key={index} className="text-body leading-relaxed text-foreground-secondary">
                  {block}
                </p>
              ))
          ) : (
            <p className="text-body text-muted-foreground">A biography has not been added yet.</p>
          )}
        </article>

        <aside aria-labelledby="public-links" className="space-y-3">
          <h2 id="public-links" className="text-h4 text-foreground">
            Public links
          </h2>
          {links.length ? (
            <ul className="space-y-2 text-small">
              {links.map((link) => (
                <li key={link.label}>
                  <a
                    href={link.href}
                    rel="noopener noreferrer"
                    target="_blank"
                    className="font-medium text-primary underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
                  >
                    {link.label}
                    <span className="sr-only"> (opens in a new tab)</span>
                  </a>
                </li>
              ))}
            </ul>
          ) : (
            <p className="text-small text-muted-foreground">No public links have been added.</p>
          )}
        </aside>
      </div>
    </>
  );
}
