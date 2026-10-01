import { Badge } from "@/components/ui/badge";
import type { ApplicationStatus } from "@/lib/applications/types";

const APPLICATION_STATUS_TONE = {
  Submitted: "info",
  Reviewed: "neutral",
  Shortlisted: "success",
  Rejected: "error",
} as const;

export function ApplicationStatusBadge({ status }: { status: ApplicationStatus }) {
  return (
    <Badge tone={APPLICATION_STATUS_TONE[status]} variant="subtle" showDot>
      {status}
    </Badge>
  );
}
