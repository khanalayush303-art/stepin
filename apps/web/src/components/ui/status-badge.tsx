import * as React from "react";
import { Badge } from "./badge";
import type { ApplicationStatus } from "@/lib/types";

/**
 * Fixed status -> tone mapping, documented on the Figma "Badge & Status" page.
 * One status, one tone, one label — identical for applicants, recruiters and admins.
 */
const STATUS_TONE = {
  Draft: "neutral",
  Submitted: "info",
  InReview: "info",
  Interview: "brand",
  Offer: "success",
  ClosingSoon: "warning",
  Unsuccessful: "error",
  Withdrawn: "neutral",
} as const;

const STATUS_LABEL: Record<ApplicationStatus, string> = {
  Draft: "Draft",
  Submitted: "Submitted",
  InReview: "In review",
  Interview: "Interview",
  Offer: "Offer",
  ClosingSoon: "Closing soon",
  Unsuccessful: "Unsuccessful",
  Withdrawn: "Withdrawn",
};

export function StatusBadge({ status }: { status: ApplicationStatus }) {
  return (
    <Badge tone={STATUS_TONE[status]} variant="subtle" showDot>
      {STATUS_LABEL[status]}
    </Badge>
  );
}

export { STATUS_LABEL };
