import Link from "next/link";
import { Building2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { VerifiedBadge } from "./verified-badge";
import type { Company } from "@/lib/types";

export function CompanyCard({ company }: { company: Company }) {
  return (
    <Card interactive className="flex flex-col items-center gap-4 p-6 text-center">
      <span
        className="flex size-16 items-center justify-center rounded-lg bg-primary-subtle text-primary"
        aria-hidden="true"
      >
        <Building2 className="size-8" />
      </span>

      <div className="flex flex-col items-center gap-1.5">
        <h3 className="text-h4 text-foreground">
          <Link
            href={`/companies/${company.slug}`}
            className="rounded-sm after:absolute after:inset-0 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
          >
            {company.name}
          </Link>
        </h3>
        <VerifiedBadge verified={company.verified} label="Verified employer" />
      </div>

      <p className="text-small text-muted-foreground">
        {company.industry} &middot; {company.size} &middot; {company.location}
      </p>

      <ul className="flex flex-wrap justify-center gap-2">
        {company.tags.map((tag) => (
          <li
            key={tag}
            className="rounded-sm bg-muted px-2 py-1 text-caption text-foreground-secondary"
          >
            {tag}
          </li>
        ))}
      </ul>

      <div className="mt-auto flex w-full items-center justify-between gap-3 border-t border-border pt-4">
        <span className="text-small font-medium text-foreground">
          {company.openRoles} open roles
        </span>
        <Button size="sm" variant="outline" asChild className="relative z-10">
          <Link href={`/companies/${company.slug}`} tabIndex={-1}>
            View company
          </Link>
        </Button>
      </div>
    </Card>
  );
}
