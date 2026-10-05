import Link from "next/link";
import { EmptyState } from "@/components/ui/empty-state";
import { Button } from "@/components/ui/button";

/** Shown for unknown or unpublished AIDX slugs. It never says whether an unpublished item exists. */
export default function AidxNotFound() {
  return (
    <div className="container-page py-16">
      <EmptyState
        icon={<span aria-hidden="true" className="text-h2">404</span>}
        title="We couldn't find that AIDX page"
        description="It may have moved, or it isn't published. Browse the lab's sections to find what you need."
        action={
          <Button asChild>
            <Link href="/aidx">Back to AIDX Lab</Link>
          </Button>
        }
      />
    </div>
  );
}
