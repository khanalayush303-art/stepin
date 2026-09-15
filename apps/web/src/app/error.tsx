"use client";

import { AlertCircle } from "lucide-react";
import { Button } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";

export default function GlobalError({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  return (
    <div className="container-page flex min-h-screen items-center justify-center py-20">
      <EmptyState
        tone="error"
        icon={<AlertCircle className="size-6" />}
        title="Something went wrong on our side"
        description="This is not something you did. Try again, and if it keeps happening the reference below helps us find it."
        action={
          <div className="flex flex-col items-center gap-3">
            <Button onClick={reset}>Try again</Button>
            {error.digest ? (
              <p className="text-caption text-muted-foreground">Reference: {error.digest}</p>
            ) : null}
          </div>
        }
      />
    </div>
  );
}
