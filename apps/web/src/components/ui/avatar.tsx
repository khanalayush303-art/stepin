"use client";

import * as React from "react";
import * as AvatarPrimitive from "@radix-ui/react-avatar";
import { cva, type VariantProps } from "class-variance-authority";
import { cn } from "@/lib/utils";

const avatarVariants = cva(
  "relative flex shrink-0 overflow-hidden rounded-full bg-primary-subtle",
  {
    variants: {
      size: {
        xs: "size-6 text-caption",
        sm: "size-8 text-caption",
        md: "size-10 text-small",
        lg: "size-14 text-h4",
      },
    },
    defaultVariants: { size: "md" },
  }
);

export interface AvatarProps
  extends React.ComponentPropsWithoutRef<typeof AvatarPrimitive.Root>,
    VariantProps<typeof avatarVariants> {
  src?: string;
  name: string;
}

export function Avatar({ className, size, src, name, ...props }: AvatarProps) {
  const initials = name
    .split(" ")
    .map((part) => part[0])
    .filter(Boolean)
    .slice(0, 2)
    .join("")
    .toUpperCase();

  return (
    <AvatarPrimitive.Root className={cn(avatarVariants({ size }), className)} {...props}>
      {src ? (
        <AvatarPrimitive.Image src={src} alt="" className="size-full object-cover" />
      ) : null}
      <AvatarPrimitive.Fallback
        delayMs={src ? 400 : 0}
        className="flex size-full items-center justify-center font-medium text-primary-subtle-fg"
      >
        <span aria-hidden="true">{initials}</span>
        <span className="sr-only">{name}</span>
      </AvatarPrimitive.Fallback>
    </AvatarPrimitive.Root>
  );
}
