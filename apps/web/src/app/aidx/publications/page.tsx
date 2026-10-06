import type { Metadata } from "next";
import Link from "next/link";
import { PageHeader } from "@/components/layout/page-header";
import {
  AidxEmptyState,
  AidxErrorState,
  AidxFilterForm,
  AidxPagination,
  FilterField,
  ResultCount,
  filterInputClass,
} from "@/components/aidx/aidx-ui";
import { Badge } from "@/components/ui/badge";
import { trySection, parsePage, query, type PageResult } from "@/lib/aidx/api";
import { labels, PUBLICATION_TYPES, safeHttpUrl } from "@/lib/aidx/format";
import type { Publication, ResearchArea } from "@/lib/aidx/types";

export const metadata: Metadata = {
  title: "Publications",
  description: "Publications from AIDX Lab, filterable by year, type, research area and keyword.",
  alternates: { canonical: "/aidx/publications" },
};

type Params = { year?: string; type?: string; area?: string; q?: string; page?: string };

export default async function PublicationsPage({ searchParams }: { searchParams: Promise<Params> }) {
  const params = await searchParams;
  const page = parsePage(params.page);
  const year = params.year && /^\d{4}$/.test(params.year) ? params.year : undefined;
  const type = PUBLICATION_TYPES.includes(params.type ?? "") ? params.type : undefined;

  const [publications, areas] = await Promise.all([
    trySection<PageResult<Publication>>(
      `/publications${query({ year, type, area: params.area, q: params.q?.trim(), page, pageSize: 12 })}`,
    ),
    trySection<ResearchArea[]>("/research"),
  ]);

  const filtered = Boolean(year || type || params.area || params.q);

  return (
    <>
      <PageHeader
        crumbs={[{ label: "AIDX Lab", href: "/aidx" }, { label: "Publications" }]}
        title="Publications"
        description="Journal articles, conference papers, reports and datasets from the lab."
      />

      <div className="container-page space-y-8 py-12">
        <AidxFilterForm label="Filter publications">
          <FilterField id="q" label="Keyword">
            <input id="q" name="q" type="search" defaultValue={params.q ?? ""} className={filterInputClass} />
          </FilterField>
          <FilterField id="year" label="Year">
            <input id="year" name="year" type="number" min={1900} max={2100} inputMode="numeric" defaultValue={year ?? ""} className={filterInputClass} />
          </FilterField>
          <FilterField id="type" label="Type">
            <select id="type" name="type" defaultValue={type ?? ""} className={filterInputClass}>
              <option value="">All types</option>
              {PUBLICATION_TYPES.map((value) => (
                <option key={value} value={value}>
                  {labels.publication(value)}
                </option>
              ))}
            </select>
          </FilterField>
          <FilterField id="area" label="Research area">
            <select id="area" name="area" defaultValue={params.area ?? ""} className={filterInputClass}>
              <option value="">All areas</option>
              {(areas.data ?? []).map((area) => (
                <option key={area.id} value={area.slug}>
                  {area.name}
                </option>
              ))}
            </select>
          </FilterField>
        </AidxFilterForm>

        {publications.failed ? (
          <AidxErrorState what="publications" />
        ) : !publications.data?.items.length ? (
          <AidxEmptyState
            title={filtered ? "No publications match your filters" : "No publications listed yet"}
            description={filtered ? "Try a different year, type or keyword." : "Publications will be listed here once added."}
          />
        ) : (
          <>
            <ResultCount total={publications.data.totalCount} noun="publication" />
            <ol className="divide-y divide-border rounded-lg border border-border bg-surface">
              {publications.data.items.map((pub) => (
                <li key={pub.id} className="space-y-3 p-6">
                  <div className="flex flex-wrap items-center gap-2 text-caption text-muted-foreground">
                    <span>{pub.year}</span>
                    <Badge tone="neutral" variant="subtle">
                      {labels.publication(pub.publicationType)}
                    </Badge>
                  </div>
                  <h2 className="text-h4 text-foreground">
                    {safeHttpUrl(pub.externalUrl) ? (
                      <a
                        href={safeHttpUrl(pub.externalUrl) ?? undefined}
                        rel="noopener noreferrer"
                        target="_blank"
                        className="underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
                      >
                        {pub.title}
                        <span className="sr-only"> (opens in a new tab)</span>
                      </a>
                    ) : (
                      pub.title
                    )}
                  </h2>
                  {pub.authors.length ? <p className="text-small text-foreground-secondary">{pub.authors.join(", ")}</p> : null}
                  <p>
                    <Link
                      href={`/aidx/publications/${pub.id}`}
                      className="text-small font-medium text-primary underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
                    >
                      View details
                      <span className="sr-only"> about {pub.title}</span>
                    </Link>
                  </p>
                  {pub.venue ? <p className="text-small text-muted-foreground">{pub.venue}</p> : null}
                  {pub.doi ? (
                    <p className="text-caption text-muted-foreground">
                      DOI: <span className="break-all">{pub.doi}</span>
                    </p>
                  ) : null}
                </li>
              ))}
            </ol>
            <AidxPagination
              result={publications.data}
              basePath="/aidx/publications"
              label="Publications"
              params={{ year, type, area: params.area, q: params.q }}
            />
          </>
        )}
      </div>
    </>
  );
}
