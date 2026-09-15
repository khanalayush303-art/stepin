import * as React from "react";
import { cva, type VariantProps } from "class-variance-authority";
import { cn } from "@/lib/utils";

/**
 * Badge — mirrors the Figma "Badge" component set (Tone x Style).
 * A badge always carries a text label; the dot is a second, non-colour cue.
 */
const badgeVariants = cva(
  "inline-flex items-center gap-1.5 rounded-sm px-2 py-1 text-caption font-medium",
  {
    variants: {
      tone: {
        neutral: "",
        brand: "",
        verified: "",
        success: "",
        warning: "",
        error: "",
        info: "",
      },
      variant: { subtle: "", solid: "" },
    },
    compoundVariants: [
      { tone: "neutral", variant: "subtle", class: "bg-muted text-foreground-secondary" },
      { tone: "neutral", variant: "solid", class: "bg-secondary text-secondary-foreground" },
      { tone: "brand", variant: "subtle", class: "bg-primary-subtle text-primary-subtle-fg" },
      { tone: "brand", variant: "solid", class: "bg-primary text-primary-foreground" },
      { tone: "verified", variant: "subtle", class: "bg-accent-subtle text-accent-subtle-fg" },
      { tone: "verified", variant: "solid", class: "bg-accent text-accent-foreground" },
      { tone: "success", variant: "subtle", class: "bg-success-subtle text-success-subtle-fg" },
      { tone: "success", variant: "solid", class: "bg-success text-on-brand" },
      { tone: "warning", variant: "subtle", class: "bg-warning-subtle text-warning-subtle-fg" },
      { tone: "warning", variant: "solid", class: "bg-warning text-on-brand" },
      { tone: "error", variant: "subtle", class: "bg-error-subtle text-error-subtle-fg" },
      { tone: "error", variant: "solid", class: "bg-error text-on-brand" },
      { tone: "info", variant: "subtle", class: "bg-info-subtle text-info-subtle-fg" },
      { tone: "info", variant: "solid", class: "bg-info text-on-brand" },
    ],
    defaultVariants: { tone: "neutral", variant: "subtle" },
  }
);

export interface BadgeProps
  extends React.HTMLAttributes<HTMLSpanElement>,
    VariantProps<typeof badgeVariants> {
  showDot?: boolean;
  icon?: React.ReactNode;
}

export function Badge({
  className,
  tone,
  variant,
  showDot = false,
  icon,
  children,
  ...props
}: BadgeProps) {
  return (
    <span className={cn(badgeVariants({ tone, variant }), className)} {...props}>
      {icon}
      {showDot && !icon ? (
        <span className="size-1.5 shrink-0 rounded-full bg-current" aria-hidden="true" />
      ) : null}
      {children}
    </span>
  );
}

export { badgeVariants };
