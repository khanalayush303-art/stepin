"use client";

import * as React from "react";
import { FileText } from "lucide-react";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { FormField } from "@/components/ui/form-field";
import { Input } from "@/components/ui/input";

export interface AidxPdfUploadProps {
  pdfUrl: string | null;
  disabled?: boolean;
  onUpload: (file: File) => Promise<void>;
  helpText?: string;
}

/** A reusable PDF-upload control for publication files — file picker, link to what is currently
 * stored, and its own busy/error state. */
export function AidxPdfUpload({ pdfUrl, disabled, onUpload, helpText }: AidxPdfUploadProps) {
  const [file, setFile] = React.useState<File | null>(null);
  const [uploading, setUploading] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);
  const [notice, setNotice] = React.useState<string | null>(null);
  const inputRef = React.useRef<HTMLInputElement>(null);

  async function submit() {
    if (!file) {
      setError("Choose a PDF file first.");
      return;
    }
    setUploading(true);
    setError(null);
    setNotice(null);
    try {
      await onUpload(file);
      setNotice("PDF uploaded.");
      setFile(null);
      if (inputRef.current) inputRef.current.value = "";
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : "The upload failed. Try again.");
    } finally {
      setUploading(false);
    }
  }

  return (
    <Card className="space-y-4 p-5">
      <h2 className="text-h4 text-foreground">PDF</h2>
      {helpText ? <p className="text-small text-muted-foreground">{helpText}</p> : null}

      {pdfUrl ? (
        <a
          href={pdfUrl}
          target="_blank"
          rel="noreferrer"
          className="inline-flex items-center gap-1.5 text-small font-medium text-primary underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
        >
          <FileText aria-hidden="true" className="size-4" />
          View current PDF
        </a>
      ) : (
        <p className="text-small text-muted-foreground">No PDF uploaded yet.</p>
      )}

      {notice ? (
        <Alert tone="success" title="Done">
          {notice}
        </Alert>
      ) : null}
      {error ? (
        <Alert tone="error" title="Couldn't upload this file">
          {error}
        </Alert>
      ) : null}

      <FormField id="aidx-pdf-file" label="Choose a new PDF" hint="PDF only, up to 10 MB.">
        {(props) => (
          <Input
            {...props}
            ref={inputRef}
            type="file"
            accept="application/pdf"
            disabled={disabled || uploading}
            onChange={(event) => setFile(event.target.files?.[0] ?? null)}
          />
        )}
      </FormField>

      <div className="flex justify-end">
        <Button type="button" onClick={() => void submit()} loading={uploading} loadingText="Uploading…" disabled={disabled}>
          Upload PDF
        </Button>
      </div>
    </Card>
  );
}
