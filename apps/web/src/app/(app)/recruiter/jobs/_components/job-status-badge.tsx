import { Badge } from "@/components/ui/badge";
import type { JobStatus } from "@/lib/jobs/types";

const JOB_STATUS_TONE = {
  Draft: "neutral",
  Published: "success",
  Unpublished: "warning",
} as const;

const JOB_STATUS_LABEL: Record<JobStatus, string> = {
  Draft: "Draft",
  Published: "Published",
  Unpublished: "Unpublished",
};

export function JobStatusBadge({ status }: { status: JobStatus }) {
  return (
    <Badge tone={JOB_STATUS_TONE[status]} variant="subtle" showDot>
      {JOB_STATUS_LABEL[status]}
    </Badge>
  );
}
