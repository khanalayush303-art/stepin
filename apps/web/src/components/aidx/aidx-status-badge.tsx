import { Badge, type BadgeProps } from "@/components/ui/badge";

const TONE: Record<string, NonNullable<BadgeProps["tone"]>> = {
  Draft: "neutral",
  Submitted: "info",
  UnderReview: "info",
  ChangesRequested: "warning",
  Published: "success",
  Archived: "neutral",
};

const LABEL: Record<string, string> = {
  Draft: "Draft",
  Submitted: "Submitted",
  UnderReview: "Under review",
  ChangesRequested: "Changes requested",
  Published: "Published",
  Archived: "Archived",
};

export function AidxStatusBadge({ status }: { status: string }) {
  return <Badge tone={TONE[status] ?? "neutral"}>{LABEL[status] ?? status}</Badge>;
}
