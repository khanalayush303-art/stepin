"use client";

import * as React from "react";
import { ChevronLeft, ChevronRight } from "lucide-react";
import { cn } from "@/lib/utils";

export interface PaginationProps {
  page: number;
  totalPages: number;
  onPageChange?: (page: number) => void;
  className?: string;
}

function pageWindow(page: number, total: number): (number | "gap")[] {
  if (total <= 7) return Array.from({ length: total }, (_, i) => i + 1);
  if (page <= 4) return [1, 2, 3, 4, 5, "gap", total];
  if (page >= total - 3) return [1, "gap", total - 4, total - 3, total - 2, total - 1, total];
  return [1, "gap", page - 1, page, page + 1, "gap", total];
}

export function Pagination({ page, totalPages, onPageChange, className }: PaginationProps) {
  const items = pageWindow(page, totalPages);
  const go = (n: number) => onPageChange?.(Math.min(Math.max(1, n), totalPages));

  const base =
    "inline-flex size-10 items-center justify-center rounded-md border text-small font-medium transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring";

  return (
    <nav aria-label="Pagination" className={cn("flex flex-wrap items-center justify-center gap-2", className)}>
      <button
        type="button"
        onClick={() => go(page - 1)}
        disabled={page === 1}
        aria-label="Previous page"
        className={cn(base, "border-border bg-surface text-foreground hover:bg-muted disabled:opacity-45")}
      >
        <ChevronLeft className="size-4" aria-hidden="true" />
      </button>

      {items.map((item, i) =>
        item === "gap" ? (
          <span key={`gap-${i}`} className={cn(base, "border-transparent text-muted-foreground")} aria-hidden="true">
            &hellip;
          </span>
        ) : (
          <button
            key={item}
            type="button"
            onClick={() => go(item)}
            aria-current={item === page ? "page" : undefined}
            aria-label={`Page ${item}`}
            className={cn(
              base,
              item === page
                ? "border-primary bg-primary text-primary-foreground"
                : "border-border bg-surface text-foreground hover:bg-muted"
            )}
          >
            {item}
          </button>
        )
      )}

      <button
        type="button"
        onClick={() => go(page + 1)}
        disabled={page === totalPages}
        aria-label="Next page"
        className={cn(base, "border-border bg-surface text-foreground hover:bg-muted disabled:opacity-45")}
      >
        <ChevronRight className="size-4" aria-hidden="true" />
      </button>
    </nav>
  );
}
