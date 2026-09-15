"use client";

import { MapPin, Search } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";

/** Compact search used at the top of the Jobs / Internships / Companies pages. */
export function JobSearchRow({
  keywordLabel = "Search roles",
  keywordPlaceholder = "Job title, skill or company",
  showLocation = true,
}: {
  keywordLabel?: string;
  keywordPlaceholder?: string;
  showLocation?: boolean;
}) {
  return (
    <form
      role="search"
      className="flex flex-col gap-3 sm:flex-row"
      onSubmit={(e) => e.preventDefault()}
    >
      <div className="relative flex-1">
        <label htmlFor="q" className="sr-only">
          {keywordLabel}
        </label>
        <Search
          className="pointer-events-none absolute left-4 top-1/2 size-[18px] -translate-y-1/2 text-muted-foreground"
          aria-hidden="true"
        />
        <Input id="q" name="q" placeholder={keywordPlaceholder} className="pl-11" />
      </div>

      {showLocation ? (
        <div className="relative sm:w-72">
          <label htmlFor="location" className="sr-only">
            Location
          </label>
          <MapPin
            className="pointer-events-none absolute left-4 top-1/2 size-[18px] -translate-y-1/2 text-muted-foreground"
            aria-hidden="true"
          />
          <Input id="location" name="location" placeholder="Sydney, NSW" className="pl-11" />
        </div>
      ) : null}

      <Button type="submit" className="sm:px-6">
        Search
      </Button>
    </form>
  );
}
