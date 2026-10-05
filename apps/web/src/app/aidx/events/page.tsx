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
  AIDX_ICONS,
  filterInputClass,
} from "@/components/aidx/aidx-ui";
import { trySection, parsePage, query, type PageResult } from "@/lib/aidx/api";
import { formatDateTime } from "@/lib/aidx/format";
import type { AidxEvent } from "@/lib/aidx/types";

export const metadata: Metadata = {
  title: "Events",
  description: "Talks, workshops and events from AIDX Lab.",
  alternates: { canonical: "/aidx/events" },
};

type Params = { upcoming?: string; page?: string };

export default async function EventsPage({ searchParams }: { searchParams: Promise<Params> }) {
  const params = await searchParams;
  const page = parsePage(params.page);
  const upcoming = params.upcoming === "true";

  const events = await trySection<PageResult<AidxEvent>>(
    `/events${query({ upcoming: upcoming ? true : undefined, page, pageSize: 12 })}`,
  );

  return (
    <>
      <PageHeader crumbs={[{ label: "AIDX Lab", href: "/aidx" }, { label: "Events" }]} title="Events" description="Talks, workshops and other events from the lab." />

      <div className="container-page space-y-8 py-12">
        <AidxFilterForm label="Filter events">
          <FilterField id="upcoming" label="Show">
            <select id="upcoming" name="upcoming" defaultValue={upcoming ? "true" : ""} className={filterInputClass}>
              <option value="">All events</option>
              <option value="true">Upcoming only</option>
            </select>
          </FilterField>
        </AidxFilterForm>

        {events.failed ? (
          <AidxErrorState what="events" />
        ) : !events.data?.items.length ? (
          <AidxEmptyState
            title={upcoming ? "No upcoming events" : "No events published yet"}
            description={upcoming ? "Check back soon, or view all events." : "Events will be listed here once published."}
          />
        ) : (
          <>
            <ResultCount total={events.data.totalCount} noun="event" />
            <ul className="grid gap-5 md:grid-cols-2">
              {events.data.items.map((event) => (
                <li key={event.id}>
                  <LinkCard
                    href={`/aidx/events/${event.slug}`}
                    title={event.title}
                    description={event.description}
                    icon={AIDX_ICONS.events}
                    meta={
                      <time dateTime={new Date(event.startsAt).toISOString()}>{formatDateTime(event.startsAt)}</time>
                    }
                  >
                    <p className="text-small text-muted-foreground">{event.location ?? "Location to be confirmed"}</p>
                  </LinkCard>
                </li>
              ))}
            </ul>
            <AidxPagination result={events.data} basePath="/aidx/events" label="Events" params={{ upcoming: upcoming ? "true" : undefined }} />
          </>
        )}
      </div>
    </>
  );
}
