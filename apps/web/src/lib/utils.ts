import { clsx, type ClassValue } from "clsx";
import { extendTailwindMerge } from "tailwind-merge";

/**
 * tailwind-merge has to be taught this project's custom theme scales.
 *
 * Without this, `text-small` (a font size in our type ramp) is classified as a
 * *text colour* and silently wins over `text-primary-foreground`, so every
 * primary button renders body-coloured slate text on an indigo fill — a 2.25:1
 * contrast failure that looks fine in isolation and only shows up in an audit.
 * The same trap applies to the custom radius, shadow and control-height scales.
 */
const FONT_SIZES = [
  "display",
  "h1",
  "h2",
  "h3",
  "h4",
  "body-lg",
  "body",
  "small",
  "caption",
  "overline",
];

const CONTROL_HEIGHTS = ["control-sm", "control-md", "control-lg"];

const twMerge = extendTailwindMerge({
  extend: {
    classGroups: {
      "font-size": [{ text: FONT_SIZES }],
      shadow: [{ shadow: ["xs", "sm", "md", "lg", "xl", "card-hover"] }],
      rounded: [{ rounded: ["none", "sm", "md", "lg", "xl", "2xl", "full"] }],
      h: [{ h: CONTROL_HEIGHTS }],
      w: [{ w: CONTROL_HEIGHTS }],
    },
  },
});

/** Merge conditional class names, with later Tailwind utilities winning. */
export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}

/** Format a date for display in AU English, e.g. "12 Sep 2026". */
export function formatDate(value: string | Date) {
  const d = typeof value === "string" ? new Date(value) : value;
  return new Intl.DateTimeFormat("en-AU", {
    day: "numeric",
    month: "short",
    year: "numeric",
  }).format(d);
}

/** Relative day count used in "Closes in N days" copy. */
export function daysUntil(value: string | Date) {
  const d = typeof value === "string" ? new Date(value) : value;
  const ms = d.getTime() - Date.now();
  return Math.max(0, Math.ceil(ms / 86_400_000));
}
