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
import { formatDate } from "@/lib/aidx/format";
import type { ProjectSummary, ResearchArea } from "@/lib/aidx/types";

export const metadata: Metadata = {
  title: "Projects",
  description: "Research projects from AIDX Lab, filterable by research area and searchable by keyword.",
  alternates: { canonical: "/aidx/projects" },
};

type Params = { search?: string; area?: string; featured?: string; page?: string };

export default async function ProjectsPage({ searchParams }: { searchParams: Promise<Params> }) {
  const params = await searchParams;
  const page = parsePage(params.page);
  // Only "true" and "false" are meaningful; anything else means "any".
  const featured = params.featured === "true" || params.featured === "false" ? params.featured : undefined;

  const [projects, areas] = await Promise.all([
    trySection<PageResult<ProjectSummary>>(
      `/projects${query({ search: params.search?.trim(), area: params.area, featured, page, pageSize: 12 })}`,
    ),
    trySection<ResearchArea[]>("/research"),
  ]);

  return (
    <>
      <PageHeader
        crumbs={[{ label: "AIDX Lab", href: "/aidx" }, { label: "Projects" }]}
        title="Projects"
        description="Research projects, with their areas and the people who work on them."
      />

      <div className="container-page space-y-8 py-12">
        <AidxFilterForm label="Filter projects">
          <FilterField id="search" label="Keyword">
            <input id="search" name="search" type="search" defaultValue={params.search ?? ""} className={filterInputClass} />
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
          <FilterField id="featured" label="Featured">
            <select id="featured" name="featured" defaultValue={featured ?? ""} className={filterInputClass}>
              <option value="">Any</option>
              <option value="true">Featured only</option>
              <option value="false">Not featured</option>
            </select>
          </FilterField>
        </AidxFilterForm>

        {projects.failed ? (
          <AidxErrorState what="projects" />
        ) : !projects.data?.items.length ? (
          <AidxEmptyState
            title={params.search || params.area || featured ? "No projects match your filters" : "No projects published yet"}
            description={params.search || params.area || featured ? "Try a different keyword or clear the filters." : "Projects will be listed here once published."}
          />
        ) : (
          <>
            <ResultCount total={projects.data.totalCount} noun="project" />
            <ul className="grid gap-5 md:grid-cols-2 xl:grid-cols-3">
              {projects.data.items.map((project) => (
                <li key={project.id}>
                  <LinkCard
                    href={`/aidx/projects/${project.slug}`}
                    title={project.title}
                    description={project.shortDescription}
                    icon={AIDX_ICONS.projects}
                    meta={[project.featured ? "Featured" : null, formatDate(project.startDate)].filter(Boolean).join(" · ") || undefined}
                  >
                    <Chips items={project.researchAreas} />
                  </LinkCard>
                </li>
              ))}
            </ul>
            <AidxPagination
              result={projects.data}
              basePath="/aidx/projects"
              label="Projects"
              params={{ search: params.search, area: params.area, featured }}
            />
          </>
        )}
      </div>
    </>
  );
}
