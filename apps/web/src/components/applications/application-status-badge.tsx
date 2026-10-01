import { Badge } from "@/components/ui/badge";
import type { ApplicationStatus } from "@/lib/applications/types";

const APPLICATION_STATUS_TONE = {
  Submitted: "info",
  Reviewed: "neutral",
  Shortlisted: "success",
  Rejected: "error",
} as const;

/**
 * Shared between the candidate's own application pages and the recruiter's
 * application review pages — same real ApplicationStatus value either way,
 * never the unrelated mock ApplicationStage type in lib/types.ts.
 */
export function ApplicationStatusBadge({ status }: { status: ApplicationStatus }) {
  return (
    <Badge tone={APPLICATION_STATUS_TONE[status]} variant="subtle" showDot>
      {status}
    </Badge>
  );
}
