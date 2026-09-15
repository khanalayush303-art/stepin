import { BadgeCheck, CircleDashed } from "lucide-react";
import { Badge } from "@/components/ui/badge";

/**
 * Verification is always explicit. An unverified employer gets a neutral
 * "Unverified" badge rather than the silent absence of one.
 */
export function VerifiedBadge({ verified, label }: { verified: boolean; label?: string }) {
  return verified ? (
    <Badge tone="verified" icon={<BadgeCheck className="size-3.5" aria-hidden="true" />}>
      {label ?? "Verified"}
    </Badge>
  ) : (
    <Badge tone="neutral" icon={<CircleDashed className="size-3.5" aria-hidden="true" />}>
      Unverified
    </Badge>
  );
}
