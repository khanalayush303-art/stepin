"use client";

import * as React from "react";
import { Button } from "@/components/ui/button";

/**
 * Plain string props only — formatting (APA text, BibTeX) happens server-side in
 * lib/aidx/citation.ts before this ever renders, so this island does nothing but display text and
 * copy it. Clipboard access can fail (permissions, non-secure context) and must not crash the page.
 */
export function AidxCitationBlock({ label, text }: { label: string; text: string }) {
  const [copied, setCopied] = React.useState(false);

  async function copy() {
    try {
      await navigator.clipboard.writeText(text);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      // Clipboard access denied or unavailable — the text is still visible to select and copy by hand.
    }
  }

  return (
    <div className="space-y-2">
      <div className="flex items-center justify-between">
        <span className="text-small font-medium text-foreground">{label}</span>
        <Button variant="ghost" size="sm" onClick={copy}>
          {copied ? "Copied" : "Copy"}
        </Button>
      </div>
      <pre className="overflow-x-auto whitespace-pre-wrap rounded-md border border-border bg-muted p-3 text-caption text-foreground-secondary">
        {text}
      </pre>
    </div>
  );
}
