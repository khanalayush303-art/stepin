import type { Metadata } from "next";
import { PageHeader } from "@/components/layout/page-header";
import { AidxEmptyState, AidxErrorState, AidxFilterForm, FilterField, LinkCard, ResultCount, filterInputClass, AIDX_ICONS } from "@/components/aidx/aidx-ui";
import { trySection } from "@/lib/aidx/api";
import type { ResearchArea } from "@/lib/aidx/types";

export const metadata: Metadata = {
  title: "Research",
  description: "The research areas AIDX Lab works in.",
  alternates: { canonical: "/aidx/research" },
};

export default async function ResearchPage({ searchParams }: { searchParams: Promise<{ search?: string }> }) {
  const { search } = await searchParams;
  const term = search?.trim() || undefined;
  const path = term ? `/research?search=${encodeURIComponent(term)}` : "/research";
  const result = await trySection<ResearchArea[]>(path);

  return (
    <>
      <PageHeader
        crumbs={[{ label: "AIDX Lab", href: "/aidx" }, { label: "Research" }]}
        title="Research areas"
        description="The fields of artificial intelligence and data science that AIDX Lab investigates."
      />

      <div className="container-page space-y-8 py-12">
        <AidxFilterForm label="Search research areas">
          <FilterField id="search" label="Search by name">
            <input id="search" name="search" type="search" defaultValue={term ?? ""} className={filterInputClass} />
          </FilterField>
        </AidxFilterForm>

        {result.failed ? (
          <AidxErrorState what="research areas" />
        ) : !result.data?.length ? (
          <AidxEmptyState
            title={term ? "No research areas match your search" : "No research areas published yet"}
            description={term ? "Try a different search term or clear the filter." : "Research areas will appear here once the lab publishes them."}
          />
        ) : (
          <>
            <ResultCount total={result.data.length} noun="research area" />
            <ul className="grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
              {result.data.map((area) => (
                <li key={area.id}>
                  <LinkCard href={`/aidx/research/${area.slug}`} title={area.name} description={area.description} icon={AIDX_ICONS.research} />
                </li>
              ))}
            </ul>
          </>
        )}
      </div>
    </>
  );
}
