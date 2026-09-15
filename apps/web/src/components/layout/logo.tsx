import Link from "next/link";
import { GraduationCap } from "lucide-react";
import { cn } from "@/lib/utils";

export function Logo({
  href = "/",
  className,
  tone = "default",
}: {
  href?: string;
  className?: string;
  tone?: "default" | "inverse";
}) {
  return (
    <Link
      href={href}
      className={cn("inline-flex items-center gap-2.5 rounded-md", className)}
    >
      <span
        className={cn(
          "flex size-8 items-center justify-center rounded-md",
          tone === "inverse" ? "bg-on-brand text-primary" : "bg-primary text-primary-foreground"
        )}
        aria-hidden="true"
      >
        <GraduationCap className="size-[18px]" />
      </span>
      <span
        className={cn(
          "text-h4",
          tone === "inverse" ? "text-on-brand" : "text-foreground"
        )}
      >
        StepIn
      </span>
    </Link>
  );
}
