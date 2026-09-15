import Link from "next/link";
import { Compass } from "lucide-react";
import { Button } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";

export default function NotFound() {
  return (
    <div className="container-page flex min-h-screen items-center justify-center py-20">
      <EmptyState
        icon={<Compass className="size-6" />}
        title="We could not find that page"
        description="The link may be out of date, or the role may have closed. Browsing all open roles is the quickest way back."
        action={
          <Button asChild>
            <Link href="/jobs">Browse all roles</Link>
          </Button>
        }
      />
    </div>
  );
}
