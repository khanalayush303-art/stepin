import { Badge } from "@/components/ui/badge";
import type { AidxProjectStatus } from "@/lib/aidx/cms";

const TONE = {
  Draft: "neutral",
  Published: "success",
  Archived: "warning",
} as const;

/** Status is always written out as text, never conveyed by colour alone. */
export function ProjectStatusBadge({ status }: { status: AidxProjectStatus }) {
  return (
    <Badge tone={TONE[status]} variant="subtle" showDot>
      {status}
    </Badge>
  );
}
