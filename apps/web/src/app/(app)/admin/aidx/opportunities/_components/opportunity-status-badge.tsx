import { Badge } from "@/components/ui/badge";
import type { AidxOpportunityStatus } from "@/lib/aidx/admin";

const TONE = {
  Draft: "neutral",
  Published: "success",
  Unpublished: "warning",
} as const;

/** Status is always shown as text, never colour alone. */
export function OpportunityStatusBadge({ status }: { status: AidxOpportunityStatus }) {
  return (
    <Badge tone={TONE[status]} variant="subtle" showDot>
      {status}
    </Badge>
  );
}
