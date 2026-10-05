import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { PageHeader } from "@/components/layout/page-header";
import { AidxErrorState } from "@/components/aidx/aidx-ui";
import { aidxGet, AidxUnavailableError } from "@/lib/aidx/api";
import type { ResearchArea } from "@/lib/aidx/types";

type Props = { params: Promise<{ slug: string }> };

async function loadArea(slug: string): Promise<ResearchArea | null> {
  return aidxGet<ResearchArea>(`/research/${encodeURIComponent(slug)}`);
}

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const { slug } = await params;
  try {
    const area = await loadArea(slug);
    if (!area) return { title: "Research area not found" };
    return {
      title: area.name,
      description: area.description ?? `${area.name}: a research area at AIDX Lab.`,
      alternates: { canonical: `/aidx/research/${area.slug}` },
    };
  } catch {
    return { title: "Research area" };
  }
}

export default async function ResearchAreaPage({ params }: Props) {
  const { slug } = await params;

  let area: ResearchArea | null;
  try {
    area = await loadArea(slug);
  } catch (error) {
    if (error instanceof AidxUnavailableError) {
      return (
        <div className="container-page py-12">
          <AidxErrorState what="this research area" />
        </div>
      );
    }
    throw error;
  }

  if (!area) notFound();

  return (
    <>
      <PageHeader
        crumbs={[{ label: "AIDX Lab", href: "/aidx" }, { label: "Research", href: "/aidx/research" }, { label: area.name }]}
        title={area.name}
        description={area.description ?? undefined}
      />
      <div className="container-page py-12">
        <p className="max-w-2xl text-body text-muted-foreground">
          Projects and people linked to this area appear on the{" "}
          <Link href="/aidx/projects" className="font-medium text-primary underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring">
            projects page
          </Link>{" "}
          when they are filtered by area.
        </p>
      </div>
    </>
  );
}
