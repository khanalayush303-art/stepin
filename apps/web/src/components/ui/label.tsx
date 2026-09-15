"use client";

import * as React from "react";
import * as LabelPrimitive from "@radix-ui/react-label";
import { cn } from "@/lib/utils";

export const Label = React.forwardRef<
  React.ComponentRef<typeof LabelPrimitive.Root>,
  React.ComponentPropsWithoutRef<typeof LabelPrimitive.Root> & { required?: boolean }
>(function Label({ className, required, children, ...props }, ref) {
  return (
    <LabelPrimitive.Root
      ref={ref}
      className={cn("text-small font-medium text-foreground", className)}
      {...props}
    >
      {children}
      {required ? (
        <>
          {" "}
          <span className="text-error" aria-hidden="true">
            *
          </span>
          <span className="sr-only">(required)</span>
        </>
      ) : null}
    </LabelPrimitive.Root>
  );
});
