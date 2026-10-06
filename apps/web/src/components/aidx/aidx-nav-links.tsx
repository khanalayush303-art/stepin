"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";

export type AidxNavItem = { href: string; label: string };

/**
 * The AIDX section links. The current section gets aria-current="page" and a visible underline,
 * so the active area is clear to sighted and screen-reader users alike.
 */
export function AidxNavLinks({ items }: { items: AidxNavItem[] }) {
  const pathname = usePathname();

  return (
    <ul className="flex flex-wrap gap-x-5 gap-y-2">
      {items.map((item) => {
        const active = pathname === item.href || pathname.startsWith(`${item.href}/`);
        return (
          <li key={item.href}>
            <Link
              href={item.href}
              aria-current={active ? "page" : undefined}
              className={`rounded-sm text-small font-medium underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring ${
                active ? "text-foreground underline" : "text-foreground-secondary hover:text-foreground"
              }`}
            >
              {item.label}
            </Link>
          </li>
        );
      })}
    </ul>
  );
}
