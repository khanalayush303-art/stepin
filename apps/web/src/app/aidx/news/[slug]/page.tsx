import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { PageHeader } from "@/components/layout/page-header";
import { AidxErrorState } from "@/components/aidx/aidx-ui";
import { aidxGet, AidxUnavailableError } from "@/lib/aidx/api";
import { formatDate } from "@/lib/aidx/format";
import type { NewsDetail } from "@/lib/aidx/types";

type Props = { params: Promise<{ slug: string }> };

async function loadNews(slug: string): Promise<NewsDetail | null> {
  return aidxGet<NewsDetail>(`/news/${encodeURIComponent(slug)}`);
}

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const { slug } = await params;
  try {
    const item = await loadNews(slug);
    if (!item) return { title: "News not found" };
    return {
      title: item.title,
      description: item.summary,
      alternates: { canonical: `/aidx/news/${item.slug}` },
      openGraph: { title: item.title, description: item.summary, type: "article", url: `/aidx/news/${item.slug}` },
    };
  } catch {
    return { title: "News" };
  }
}

export default async function NewsItemPage({ params }: Props) {
  const { slug } = await params;

  let item: NewsDetail | null;
  try {
    item = await loadNews(slug);
  } catch (error) {
    if (error instanceof AidxUnavailableError) {
      return (
        <div className="container-page py-12">
          <AidxErrorState what="this news item" />
        </div>
      );
    }
    throw error;
  }

  if (!item) notFound();

  const published = formatDate(item.publishedAt);

  return (
    <>
      <PageHeader
        crumbs={[{ label: "AIDX Lab", href: "/aidx" }, { label: "News", href: "/aidx/news" }, { label: item.title }]}
        title={item.title}
        description={item.summary}
      >
        {published && item.publishedAt ? (
          <time dateTime={new Date(item.publishedAt).toISOString()} className="text-small text-muted-foreground">
            Published {published}
          </time>
        ) : null}
      </PageHeader>

      <article className="container-page max-w-3xl space-y-5 py-12">
        {item.body
          .split(/\n\s*\n/)
          .map((block) => block.trim())
          .filter(Boolean)
          .map((block, index) => (
            <p key={index} className="text-body-lg leading-relaxed text-foreground-secondary">
              {block}
            </p>
          ))}
      </article>
    </>
  );
}
