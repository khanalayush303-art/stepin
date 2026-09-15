"use client";

import * as React from "react";
import { AlertCircle } from "lucide-react";
import { cn } from "@/lib/utils";
import { Label } from "./label";

export interface FormFieldProps {
  id: string;
  label: string;
  /** Persistent helper text. Never used to carry the label. */
  hint?: string;
  error?: string;
  required?: boolean;
  className?: string;
  /** Rendered on the same row as the label, e.g. a "Forgot password?" link. */
  action?: React.ReactNode;
  children: (props: {
    id: string;
    "aria-describedby": string | undefined;
    invalid: boolean;
  }) => React.ReactNode;
}

/**
 * FormField wires label, control, hint and error together.
 * The error is announced in text and marked with an icon, so the message never
 * depends on colour alone (WCAG 1.4.1).
 */
export function FormField({
  id,
  label,
  hint,
  error,
  required,
  className,
  action,
  children,
}: FormFieldProps) {
  const hintId = hint ? `${id}-hint` : undefined;
  const errorId = error ? `${id}-error` : undefined;
  const describedBy = [hintId, errorId].filter(Boolean).join(" ") || undefined;

  return (
    <div className={cn("flex flex-col gap-1.5", className)}>
      <div className="flex items-center justify-between gap-2">
        <Label htmlFor={id} required={required}>
          {label}
        </Label>
        {action}
      </div>

      {children({ id, "aria-describedby": describedBy, invalid: Boolean(error) })}

      {hint && !error ? (
        <p id={hintId} className="text-caption text-muted-foreground">
          {hint}
        </p>
      ) : null}

      {error ? (
        <p
          id={errorId}
          role="alert"
          className="flex items-center gap-1.5 text-caption text-error"
        >
          <AlertCircle className="size-3.5 shrink-0" aria-hidden="true" />
          {error}
        </p>
      ) : null}
    </div>
  );
}
