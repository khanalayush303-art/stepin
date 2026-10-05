import type { Metadata } from "next";
import { PageHeader } from "@/components/layout/page-header";
import {
  AidxEmptyState,
  AidxErrorState,
  AidxFilterForm,
  AidxPagination,
  Chips,
  FilterField,
  LinkCard,
  ResultCount,
  AIDX_ICONS,
  filterInputClass,
} from "@/components/aidx/aidx-ui";
import { trySection, parsePage, query, type PageResult } from "@/lib/aidx/api";
import { labels } from "@/lib/aidx/format";
import type { Opportunity } from "@/lib/aidx/types";

export const metadata: Metadata = {
  title: "Research opportunities",
  description: "Research roles published on StepIn. Open a role to view it and apply through StepIn.",
  alternates: { canonical: "/aidx/opportunities" },
};

const EMPLOYMENT_TYPES = ["FullTime", "PartTime", "Contract", "Internship", "Casual"];

type Params = { type?: string; page?: string };

/**
 * Research opportunities are StepIn jobs with the Research category. Each card links to the
 * existing StepIn job detail (/dashboard/discover/{id}), which is where candidates apply. AIDX
 * has no application flow of its own.
 */
export default async function OpportunitiesPage({ searchParams }: { searchParams: Promise<Params> }) {
  const params = await searchParams;
  const page = parsePage(params.page);
  const type = EMPLOYMENT_TYPES.includes(params.type ?? "") ? params.type : undefined;

  const opportunities = await trySection<PageResult<Opportunity>>(
    `/opportunities${query({ type, page, pageSize: 12 })}`,
  );

  return (
    <>
      <PageHeader
        crumbs={[{ label: "AIDX Lab", href: "/aidx" }, { label: "Opportunities" }]}
        title="Research opportunities"
        description="Research roles published on StepIn. Open a role to read the full details and apply. You need a StepIn account to apply."
      />

      <div className="container-page space-y-8 py-12">
        <AidxFilterForm label="Filter opportunities">
          <FilterField id="type" label="Employment type">
            <select id="type" name="type" defaultValue={type ?? ""} className={filterInputClass}>
              <option value="">All types</option>
              {EMPLOYMENT_TYPES.map((value) => (
                <option key={value} value={value}>
                  {labels.employment(value)}
                </option>
              ))}
            </select>
          </FilterField>
        </AidxFilterForm>

        {opportunities.failed ? (
          <AidxErrorState what="research opportunities" />
        ) : !opportunities.data?.items.length ? (
          <AidxEmptyState
            title={type ? "No research opportunities match this type" : "No research opportunities right now"}
            description={type ? "Try another employment type or clear the filter." : "New research roles are listed here as they are published."}
          />
        ) : (
          <>
            <ResultCount total={opportunities.data.totalCount} noun="opportunity" />
            <ul className="grid gap-5 md:grid-cols-2 xl:grid-cols-3">
              {opportunities.data.items.map((job) => (
                <li key={job.id}>
                  <LinkCard
                    href={`/dashboard/discover/${job.id}`}
                    title={job.title}
                    description={`${job.companyName} · ${job.location}`}
                    icon={AIDX_ICONS.opportunities}
                    meta={`Research opportunity · ${labels.employment(job.employmentType)} · ${labels.workplace(job.workplaceType)}`}
                  >
                    {job.projectTitle ? (
                      <p className="text-small text-foreground-secondary">
                        Linked project: <span className="font-medium">{job.projectTitle}</span>
                      </p>
                    ) : null}
                    <Chips items={job.skills} />
                  </LinkCard>
                </li>
              ))}
            </ul>
            <AidxPagination result={opportunities.data} basePath="/aidx/opportunities" label="Opportunities" params={{ type }} />
          </>
        )}
      </div>
    </>
  );
}
