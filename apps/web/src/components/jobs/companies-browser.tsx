"use client";

import * as React from "react";
import { AlertCircle, Building2, Search } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { EmptyState } from "@/components/ui/empty-state";
import { Input } from "@/components/ui/input";
import { Skeleton } from "@/components/ui/skeleton";
import { CompanyCard } from "./company-card";
import { cn } from "@/lib/utils";
import type { Company } from "@/lib/types";

export function CompaniesBrowser({
  companies,
  state = "ready",
}: {
  companies: Company[];
  state?: string;
}) {
  const [query, setQuery] = React.useState("");
  const [industry, setIndustry] = React.useState<string>("All");

  const industries = React.useMemo(
    () => ["All", ...Array.from(new Set(companies.map((c) => c.industry))).sort()],
    [companies]
  );

  const results = companies.filter((c) => {
    const matchesQuery = c.name.toLowerCase().includes(query.trim().toLowerCase());
    const matchesIndustry = industry === "All" || c.industry === industry;
    return matchesQuery && matchesIndustry;
  });

  const showEmpty = state === "empty" || (state !== "loading" && state !== "error" && results.length === 0);

  return (
    <section className="container-page space-y-6 py-10 lg:py-12">
      <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
        <div className="relative lg:w-96">
          <label htmlFor="company-q" className="sr-only">
            Search employers
          </label>
          <Search
            className="pointer-events-none absolute left-4 top-1/2 size-[18px] -translate-y-1/2 text-muted-foreground"
            aria-hidden="true"
          />
          <Input
            id="company-q"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder="Search employers"
            className="pl-11"
          />
        </div>

        <div role="group" aria-label="Filter by industry" className="flex flex-wrap gap-2">
          {industries.map((item) => (
            <button
              key={item}
              type="button"
              onClick={() => setIndustry(item)}
              aria-pressed={industry === item}
              className={cn(
                "rounded-full px-3.5 py-1.5 text-caption transition-colors",
                "focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring",
                industry === item
                  ? "bg-primary font-medium text-primary-foreground"
                  : "bg-muted text-foreground-secondary hover:bg-secondary-hover"
              )}
            >
              {item}
            </button>
          ))}
        </div>
      </div>

      {state === "loading" ? (
        <ul className="grid gap-6 sm:grid-cols-2 xl:grid-cols-3" aria-busy="true">
          {Array.from({ length: 6 }).map((_, i) => (
            <li key={i}>
              <Card className="flex flex-col items-center gap-4 p-6">
                <Skeleton className="size-16 rounded-lg" />
                <Skeleton className="h-4 w-32" />
                <Skeleton className="h-5 w-36 rounded-full" />
                <Skeleton className="h-3 w-48" />
                <Skeleton className="h-9 w-full" />
              </Card>
            </li>
          ))}
        </ul>
      ) : state === "error" ? (
        <EmptyState
          tone="error"
          icon={<AlertCircle className="size-6" />}
          title="We could not load employers"
          description="Something went wrong on our side, not yours. Your search is still here."
          action={<Button onClick={() => window.location.reload()}>Try again</Button>}
        />
      ) : showEmpty ? (
        <EmptyState
          icon={<Building2 className="size-6" />}
          title="No employers match that search"
          description="Check the spelling, or clear the industry filter to see everyone currently hiring."
          action={
            <Button
              onClick={() => {
                setQuery("");
                setIndustry("All");
              }}
            >
              Clear search
            </Button>
          }
        />
      ) : (
        <ul className="grid gap-6 sm:grid-cols-2 xl:grid-cols-3">
          {results.map((company) => (
            <li key={company.id} className="relative flex">
              <CompanyCard company={company} />
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
