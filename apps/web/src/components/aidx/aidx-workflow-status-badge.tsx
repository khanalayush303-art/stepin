import { Badge } from "@/components/ui/badge";
import type { AidxWorkflowStatus } from "@/lib/aidx/workflow-status";

const TONE = {
  Draft: "neutral",
  Submitted: "info",
  UnderReview: "info",
  ChangesRequested: "warning",
  Published: "success",
  Archived: "neutral",
} as const;

const LABEL: Record<AidxWorkflowStatus, string> = {
  Draft: "Draft",
  Submitted: "Submitted",
  UnderReview: "Under review",
  ChangesRequested: "Changes requested",
  Published: "Published",
  Archived: "Archived",
};

/**
 * The one status badge for every AIDX content type's full Draft/Submitted/UnderReview/
 * ChangesRequested/Published/Archived workflow — shared by the Admin CMS pages so a status this
 * page doesn't expect (e.g. a row mid-review through the membership-gated workflow, then opened
 * in the old Admin UI) still renders correctly instead of silently falling through to an
 * undefined tone. Status is always written out as text, never conveyed by colour alone.
 */
export function AidxWorkflowStatusBadge({ status }: { status: AidxWorkflowStatus | string }) {
  const known = status in TONE;
  return (
    <Badge tone={known ? TONE[status as AidxWorkflowStatus] : "neutral"} variant="subtle" showDot>
      {known ? LABEL[status as AidxWorkflowStatus] : status}
    </Badge>
  );
}
