import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { PageHeader } from "@/components/layout/page-header";
import { AidxErrorState } from "@/components/aidx/aidx-ui";
import { Button } from "@/components/ui/button";
import { aidxGet, AidxUnavailableError } from "@/lib/aidx/api";
import { formatDateTime, safeHttpUrl } from "@/lib/aidx/format";
import type { AidxEvent } from "@/lib/aidx/types";

type Props = { params: Promise<{ slug: string }> };

async function loadEvent(slug: string): Promise<AidxEvent | null> {
  return aidxGet<AidxEvent>(`/events/${encodeURIComponent(slug)}`);
}

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const { slug } = await params;
  try {
    const evt = await loadEvent(slug);
    if (!evt) return { title: "Event not found" };
    return {
      title: evt.title,
      description: evt.description.slice(0, 160),
      alternates: { canonical: `/aidx/events/${evt.slug}` },
    };
  } catch {
    return { title: "Event" };
  }
}

export default async function EventPage({ params }: Props) {
  const { slug } = await params;

  let evt: AidxEvent | null;
  try {
    evt = await loadEvent(slug);
  } catch (error) {
    if (error instanceof AidxUnavailableError) {
      return (
        <div className="container-page py-12">
          <AidxErrorState what="this event" />
        </div>
      );
    }
    throw error;
  }

  if (!evt) notFound();

  const starts = formatDateTime(evt.startsAt);
  const ends = evt.endsAt ? formatDateTime(evt.endsAt) : null;

  return (
    <>
      <PageHeader
        crumbs={[{ label: "AIDX Lab", href: "/aidx" }, { label: "Events", href: "/aidx/events" }, { label: evt.title }]}
        title={evt.title}
      >
        <dl className="grid gap-3 text-small sm:grid-cols-2">
          <div>
            <dt className="font-medium text-foreground">When</dt>
            <dd className="text-muted-foreground">
              <time dateTime={new Date(evt.startsAt).toISOString()}>{starts}</time>
              {ends && evt.endsAt ? (
                <>
                  {" "}
                  to <time dateTime={new Date(evt.endsAt).toISOString()}>{ends}</time>
                </>
              ) : null}
            </dd>
          </div>
          <div>
            <dt className="font-medium text-foreground">Where</dt>
            <dd className="text-muted-foreground">{evt.location ?? "Location to be confirmed"}</dd>
          </div>
          {evt.speakerName ? (
            <div>
              <dt className="font-medium text-foreground">Speaker</dt>
              <dd className="text-muted-foreground">{evt.speakerName}</dd>
            </div>
          ) : null}
        </dl>
      </PageHeader>

      <div className="container-page max-w-3xl space-y-8 py-12">
        <div className="space-y-5">
          {evt.description
            .split(/\n\s*\n/)
            .map((block) => block.trim())
            .filter(Boolean)
            .map((block, index) => (
              <p key={index} className="text-body-lg leading-relaxed text-foreground-secondary">
                {block}
              </p>
            ))}
        </div>

        {safeHttpUrl(evt.registrationUrl) ? (
          <Button asChild>
            <a href={safeHttpUrl(evt.registrationUrl) ?? undefined} rel="noopener noreferrer" target="_blank">
              Register
              <span className="sr-only"> (opens in a new tab)</span>
            </a>
          </Button>
        ) : null}
      </div>
    </>
  );
}
