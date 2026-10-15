import { AidxWorkflowStatusBadge } from "@/components/aidx/aidx-workflow-status-badge";
import type { AidxProjectStatus } from "@/lib/aidx/cms";

/**
 * Thin alias over the shared AidxWorkflowStatusBadge, kept under this name/path since the
 * Project/News/Event admin list and edit pages already import it from here. Previously its own
 * component limited to Draft/Published/Archived, which stopped matching reality once these
 * content types gained the full Submitted/UnderReview/ChangesRequested workflow.
 */
export function ProjectStatusBadge({ status }: { status: AidxProjectStatus }) {
  return <AidxWorkflowStatusBadge status={status} />;
}
