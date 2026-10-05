import type { Metadata } from "next";
import { PageHeader } from "@/components/layout/page-header";
import {
  AidxEmptyState,
  AidxErrorState,
  AidxFilterForm,
  AidxPagination,
  FilterField,
  LinkCard,
  ResultCount,
  filterInputClass,
} from "@/components/aidx/aidx-ui";
import { trySection, parsePage, query, type PageResult } from "@/lib/aidx/api";
import { labels, RESEARCHER_CATEGORIES } from "@/lib/aidx/format";
import type { Person } from "@/lib/aidx/types";

export const metadata: Metadata = {
  title: "People",
  description: "The researchers and people in AIDX Lab.",
  alternates: { canonical: "/aidx/people" },
};

type Params = { search?: string; category?: string; page?: string };

export default async function PeoplePage({ searchParams }: { searchParams: Promise<Params> }) {
  const params = await searchParams;
  const page = parsePage(params.page);
  const category = RESEARCHER_CATEGORIES.includes(params.category ?? "") ? params.category : undefined;

  const people = await trySection<PageResult<Person>>(
    `/people${query({ search: params.search?.trim(), category, page, pageSize: 12 })}`,
  );

  return (
    <>
      <PageHeader
        crumbs={[{ label: "AIDX Lab", href: "/aidx" }, { label: "People" }]}
        title="People"
        description="Researchers, students and alumni of AIDX Lab. Only public profile information is shown."
      />

      <div className="container-page space-y-8 py-12">
        <AidxFilterForm label="Filter people">
          <FilterField id="search" label="Name">
            <input id="search" name="search" type="search" defaultValue={params.search ?? ""} className={filterInputClass} />
          </FilterField>
          <FilterField id="category" label="Role">
            <select id="category" name="category" defaultValue={category ?? ""} className={filterInputClass}>
              <option value="">All roles</option>
              {RESEARCHER_CATEGORIES.map((value) => (
                <option key={value} value={value}>
                  {labels.researcher(value)}
                </option>
              ))}
            </select>
          </FilterField>
        </AidxFilterForm>

        {people.failed ? (
          <AidxErrorState what="people" />
        ) : !people.data?.items.length ? (
          <AidxEmptyState
            title={params.search || category ? "No people match your filters" : "No public profiles yet"}
            description={params.search || category ? "Try a different name or clear the filters." : "Researcher profiles will appear here once published."}
          />
        ) : (
          <>
            <ResultCount total={people.data.totalCount} noun="person" />
            <ul className="grid gap-5 sm:grid-cols-2 xl:grid-cols-3">
              {people.data.items.map((person) => (
                <li key={person.id}>
                  <LinkCard
                    href={`/aidx/people/${person.slug}`}
                    title={person.displayName}
                    description={person.position}
                    meta={labels.researcher(person.category)}
                  />
                </li>
              ))}
            </ul>
            <AidxPagination result={people.data} basePath="/aidx/people" label="People" params={{ search: params.search, category }} />
          </>
        )}
      </div>
    </>
  );
}
