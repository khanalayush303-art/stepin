import * as React from "react";
import { AlertCircle, CheckCircle2, Clock, Info } from "lucide-react";
import { cva, type VariantProps } from "class-variance-authority";
import { cn } from "@/lib/utils";

/** The glyph differs per tone, so the alert type is never colour alone. */
const TONE_ICON = {
  info: Info,
  success: CheckCircle2,
  warning: Clock,
  error: AlertCircle,
} as const;

const alertVariants = cva("flex gap-3 rounded-md border-l-[3px] p-4", {
  variants: {
    tone: {
      info: "border-l-info bg-info-subtle",
      success: "border-l-success bg-success-subtle",
      warning: "border-l-warning bg-warning-subtle",
      error: "border-l-error bg-error-subtle",
    },
  },
  defaultVariants: { tone: "info" },
});

const TITLE_TONE = {
  info: "text-info-subtle-fg",
  success: "text-success-subtle-fg",
  warning: "text-warning-subtle-fg",
  error: "text-error-subtle-fg",
} as const;

export interface AlertProps
  extends React.HTMLAttributes<HTMLDivElement>,
    VariantProps<typeof alertVariants> {
  title: string;
}

export function Alert({ className, tone = "info", title, children, ...props }: AlertProps) {
  const Icon = TONE_ICON[tone ?? "info"];
  return (
    <div
      role={tone === "error" ? "alert" : "status"}
      className={cn(alertVariants({ tone }), className)}
      {...props}
    >
      <Icon className={cn("mt-0.5 size-5 shrink-0", TITLE_TONE[tone ?? "info"])} aria-hidden="true" />
      <div className="flex flex-col gap-0.5">
        <p className={cn("text-small font-medium", TITLE_TONE[tone ?? "info"])}>{title}</p>
        {children ? (
          <div className="text-small text-foreground-secondary">{children}</div>
        ) : null}
      </div>
    </div>
  );
}
