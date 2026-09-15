import * as React from "react";
import { cn } from "@/lib/utils";
import { Button, type ButtonProps } from "./button";

export interface IconButtonProps extends Omit<ButtonProps, "size" | "children"> {
  /** Required: icon-only controls must still expose an accessible name. */
  label: string;
  icon: React.ReactNode;
}

/** Icon-only control. 44x44 so it clears the WCAG 2.1 AA target size. */
export const IconButton = React.forwardRef<HTMLButtonElement, IconButtonProps>(
  function IconButton({ label, icon, className, variant = "ghost", ...props }, ref) {
    return (
      <Button
        ref={ref}
        size="icon"
        variant={variant}
        aria-label={label}
        title={label}
        className={cn("rounded-md", className)}
        {...props}
      >
        {icon}
      </Button>
    );
  }
);
