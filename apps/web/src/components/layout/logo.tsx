import Link from "next/link";
import { cn } from "@/lib/utils";

/**
 * The approved StepIn: The AIDX Experience logo artwork (not recreated with text/CSS).
 * The source file has an opaque white background — fine on this app's light surfaces
 * (`bg-surface` is white; dark mode tokens exist in globals.css but nothing in the app
 * currently switches to them), but it would show as a visible white box on a solid
 * colored background. Anywhere that would happen, the surrounding surface is kept
 * light instead of forcing a colored panel behind the mark — see (auth)/layout.tsx.
 *
 * `compact` crops to just the "StepIn" ribbon lettering (full width, shorter height) —
 * the full lockup's "The AIDX Experience" / slogan lines are too fine to stay legible
 * at header/sidebar heights. `full` is the complete lockup, including the slogan,
 * for the few places with room to show it properly.
 */
const SOURCES = {
  compact: {
    src: "/brand/stepin-aidx-wordmark.png",
    width: 1610,
    height: 393,
  },
  full: {
    src: "/brand/stepin-aidx-logo-full.png",
    width: 1610,
    height: 596,
  },
} as const;

export function Logo({
  href = "/",
  className,
  variant = "compact",
  imgClassName,
}: {
  href?: string;
  className?: string;
  /** `compact` (default): the wordmark alone, for headers/sidebars. `full`: the complete
   * lockup with "The AIDX Experience" and the slogan — only where there's room for it to
   * stay legible (e.g. the auth brand panel, the site footer). */
  variant?: "compact" | "full";
  imgClassName?: string;
}) {
  const asset = SOURCES[variant];
  const alt =
    variant === "full"
      ? "StepIn: The AIDX Experience — Connecting Careers, Research & Innovation"
      : "StepIn: The AIDX Experience";

  return (
    <Link href={href} className={cn("inline-flex items-center rounded-md", className)}>
      {/* eslint-disable-next-line @next/next/no-img-element -- this app doesn't use next/image anywhere; a plain <img> matches existing convention. */}
      <img
        src={asset.src}
        alt={alt}
        width={asset.width}
        height={asset.height}
        className={cn(variant === "full" ? "h-14 w-auto sm:h-20" : "h-8 w-auto", imgClassName)}
      />
    </Link>
  );
}
