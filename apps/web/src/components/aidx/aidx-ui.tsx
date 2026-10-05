import Link from "next/link";
import { AlertTriangle, CalendarDays, FileText, FolderKanban, Newspaper, Users, Briefcase, Layers } from "lucide-react";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { EmptyState } from "@/components/ui/empty-state";
import { query } from "@/lib/aidx/api";
import type { PageResult } from "@/lib/aidx/types";
import { cn } from "@/lib/utils";

/** Section-level error. Used instead of a raw API message so no backend detail reaches the page. */
export function AidxErrorState({ what }: { what: string }) {
  return (
    <EmptyState
      tone="error"
      icon={<AlertTriangle aria-hidden="true" className="size-6" />}
      title={`We couldn't load ${what}`}
      description="The AIDX service is temporarily unavailable. Refresh the page in a moment."
    />
  );
}

export function AidxEmptyState({ title, description }: { title: string; description: string }) {
  return (
    <EmptyState
      icon={<Layers aria-hidden="true" className="size-6" />}
      title={title}
      description={description}
    />
  );
}

/** Prev/next links that keep every other filter in the URL. */
export function AidxPagination<T>({
  result,
  basePath,
  params,
  label,
}: {
  result: PageResult<T>;
  basePath: string;
  params: Record<string, string | undefined>;
  label: string;
}) {
  if (result.totalPages <= 1) return null;

  const href = (page: number) => `${basePath}${query({ ...params, page })}`;
  const hasPrevious = result.page > 1;
  const hasNext = result.page < result.totalPages;

  return (
    <nav aria-label={`${label} pages`} className="mt-10 flex flex-wrap items-center justify-between gap-4 border-t border-border pt-6">
      <p className="text-small text-muted-foreground">
        Page {result.page} of {result.totalPages} · {result.totalCount} in total
      </p>
      <div className="flex gap-3">
        {hasPrevious ? (
          <Link href={href(result.page - 1)} className={pagerClass}>
            Previous<span className="sr-only"> page</span>
          </Link>
        ) : null}
        {hasNext ? (
          <Link href={href(result.page + 1)} className={pagerClass}>
            Next<span className="sr-only"> page</span>
          </Link>
        ) : null}
      </div>
    </nav>
  );
}

const pagerClass =
  "rounded-md border border-border px-4 py-2 text-small font-medium text-foreground hover:bg-muted focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring";

/** Shared search/filter form. Plain GET, so it works without JavaScript and is bookmarkable. */
export function AidxFilterForm({ children, label }: { children: React.ReactNode; label: string }) {
  return (
    <form method="get" aria-label={label} className="grid gap-4 rounded-lg border border-border bg-surface p-5 sm:grid-cols-2 lg:grid-cols-4">
      {children}
      <div className="flex items-end gap-3 sm:col-span-2 lg:col-span-4">
        <button
          type="submit"
          className="rounded-md bg-primary px-5 py-2.5 text-small font-semibold text-primary-foreground hover:bg-primary/90 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
        >
          Apply filters
        </button>
        <Link href="?" className="text-small font-medium text-primary underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-ring">
          Clear
        </Link>
      </div>
    </form>
  );
}

export function FilterField({
  id,
  label,
  children,
}: {
  id: string;
  label: string;
  children: React.ReactNode;
}) {
  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor={id} className="text-small font-medium text-foreground">
        {label}
      </label>
      {children}
    </div>
  );
}

export const filterInputClass =
  "h-10 rounded-md border border-input bg-background px-3 text-small text-foreground focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring";

export function ResultCount({ total, noun }: { total: number; noun: string }) {
  return (
    <p className="text-small text-muted-foreground" aria-live="polite">
      {total} {total === 1 ? noun : `${noun}s`}
    </p>
  );
}

/** Short list of text chips. Never colour-only: every chip is real text. */
export function Chips({ items, tone = "neutral" }: { items: string[]; tone?: "neutral" | "brand" }) {
  if (items.length === 0) return null;
  return (
    <ul className="flex flex-wrap gap-2" aria-label="Tags">
      {items.map((item) => (
        <li key={item}>
          <Badge tone={tone} variant="subtle">
            {item}
          </Badge>
        </li>
      ))}
    </ul>
  );
}

/** A card with a single primary link. The whole title is the link, so it stays keyboard-focusable once. */
export function LinkCard({
  href,
  title,
  description,
  meta,
  icon,
  children,
  className,
}: {
  href: string;
  title: string;
  description?: string | null;
  meta?: React.ReactNode;
  icon?: React.ReactNode;
  children?: React.ReactNode;
  className?: string;
}) {
  return (
    <Card className={cn("flex h-full flex-col transition-colors hover:border-primary/60", className)}>
      <CardHeader className="gap-2">
        {icon ? <span className="text-primary">{icon}</span> : null}
        <CardTitle className="text-h4 leading-snug">
          <Link href={href} className="rounded-sm underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring">
            {title}
          </Link>
        </CardTitle>
        {meta ? <div className="text-caption text-muted-foreground">{meta}</div> : null}
      </CardHeader>
      {description || children ? (
        <CardContent className="flex flex-1 flex-col gap-3">
          {description ? <CardDescription className="text-body text-muted-foreground">{description}</CardDescription> : null}
          {children}
        </CardContent>
      ) : null}
    </Card>
  );
}

/** Icons used by the section headings, kept in one place so the visual language stays consistent. */
export const AIDX_ICONS = {
  research: <Layers aria-hidden="true" className="size-5" />,
  projects: <FolderKanban aria-hidden="true" className="size-5" />,
  people: <Users aria-hidden="true" className="size-5" />,
  publications: <FileText aria-hidden="true" className="size-5" />,
  news: <Newspaper aria-hidden="true" className="size-5" />,
  events: <CalendarDays aria-hidden="true" className="size-5" />,
  opportunities: <Briefcase aria-hidden="true" className="size-5" />,
};

/** Section heading with a consistent id so each landing section can be labelled by its heading. */
export function SectionHeading({
  id,
  title,
  description,
  href,
  linkLabel,
}: {
  id: string;
  title: string;
  description?: string;
  href?: string;
  linkLabel?: string;
}) {
  return (
    <div className="flex flex-wrap items-end justify-between gap-4">
      <div className="space-y-1.5">
        <h2 id={id} className="text-h2 text-foreground">
          {title}
        </h2>
        {description ? <p className="max-w-2xl text-body text-muted-foreground">{description}</p> : null}
      </div>
      {href && linkLabel ? (
        <Link href={href} className="text-small font-semibold text-primary underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring">
          {linkLabel}
        </Link>
      ) : null}
    </div>
  );
}
