"use client";

import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";

export default function ServiceUnavailablePage() {
  return (
    <div className="space-y-5">
      <header className="space-y-1.5">
        <h1 className="text-h2 text-foreground">We couldn&apos;t load your account</h1>
        <p className="text-body text-muted-foreground">
          You&apos;re signed in, but StepIn couldn&apos;t reach the server just now. This isn&apos;t about your
          account — it&apos;s a temporary problem on our side.
        </p>
      </header>

      <Alert tone="warning" title="Temporary problem">
        Try again in a moment. If this keeps happening, contact support.
      </Alert>

      <Button type="button" size="lg" onClick={() => window.location.reload()}>
        Try again
      </Button>
    </div>
  );
}
