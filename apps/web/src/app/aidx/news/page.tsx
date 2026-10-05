import type { Metadata } from "next";
import { PageHeader } from "@/components/layout/page-header";
import { AidxEmptyState, AidxErrorState, AidxPagination, LinkCard, ResultCount, AIDX_ICONS } from "@/components/aidx/aidx-ui";
import { trySection, parsePage, query, type PageResult } from "@/lib/aidx/api";
import { formatDate } from "@/lib/aidx/format";
import type { NewsSummary } from "@/lib/aidx/types";

export const metadata: Metadata = {
  title: "News",
  description: "Latest news from AIDX Lab.",
  alternates: { canonical: "/aidx/news" },
};

export default async function NewsPage({ searchParams }: { searchParams: Promise<{ page?: string }> }) {
  const { page: rawPage } = await searchParams;
  const page = parsePage(rawPage);
  const news = await trySection<PageResult<NewsSummary>>(`/news${query({ page, pageSize: 10 })}`);

  return (
    <>
      <PageHeader crumbs={[{ label: "AIDX Lab", href: "/aidx" }, { label: "News" }]} title="News" description="The latest from AIDX Lab." />

      <div className="container-page space-y-8 py-12">
        {news.failed ? (
          <AidxErrorState what="news" />
        ) : !news.data?.items.length ? (
          <AidxEmptyState title="No news published yet" description="Lab news will appear here as it is published." />
        ) : (
          <>
            <ResultCount total={news.data.totalCount} noun="news item" />
            <ul className="space-y-5">
              {news.data.items.map((item) => (
                <li key={item.id}>
                  <LinkCard
                    href={`/aidx/news/${item.slug}`}
                    title={item.title}
                    description={item.summary}
                    meta={formatDate(item.publishedAt)}
                    icon={AIDX_ICONS.news}
                  />
                </li>
              ))}
            </ul>
            <AidxPagination result={news.data} basePath="/aidx/news" label="News" params={{}} />
          </>
        )}
      </div>
    </>
  );
}
