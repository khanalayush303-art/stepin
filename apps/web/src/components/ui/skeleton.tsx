import { cn } from "@/lib/utils";

/** Skeletons mirror the real layout so nothing shifts when content lands. */
export function Skeleton({ className, ...props }: React.HTMLAttributes<HTMLDivElement>) {
  return <div className={cn("skeleton rounded-sm", className)} aria-hidden="true" {...props} />;
}

export function JobCardSkeleton() {
  return (
    <div className="rounded-lg border border-border bg-card p-6">
      <div className="flex items-center gap-3">
        <Skeleton className="size-12 rounded-md" />
        <div className="flex-1 space-y-2">
          <Skeleton className="h-4 w-1/2" />
          <Skeleton className="h-3 w-1/3" />
        </div>
      </div>
      <div className="mt-4 space-y-2">
        <Skeleton className="h-3 w-full" />
        <Skeleton className="h-3 w-full" />
        <Skeleton className="h-3 w-3/5" />
      </div>
      <div className="mt-4 flex gap-2">
        <Skeleton className="h-6 w-14 rounded-sm" />
        <Skeleton className="h-6 w-16 rounded-sm" />
        <Skeleton className="h-6 w-20 rounded-sm" />
      </div>
    </div>
  );
}

export function JobListSkeleton({ count = 4 }: { count?: number }) {
  return (
    <div className="space-y-4" role="status" aria-live="polite" aria-busy="true">
      <span className="sr-only">Loading roles</span>
      {Array.from({ length: count }).map((_, i) => (
        <JobCardSkeleton key={i} />
      ))}
    </div>
  );
}
