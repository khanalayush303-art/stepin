"use client";

import * as React from "react";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { FormField } from "@/components/ui/form-field";
import { Input } from "@/components/ui/input";

export interface AidxImageUploadProps {
  /** Current image URL (a convention-based media route), or null if none has been uploaded yet. */
  imageUrl: string | null;
  currentAltText: string | null;
  disabled?: boolean;
  /** Performs the actual upload call. Thrown errors are shown inline; the file input is not cleared on failure. */
  onUpload: (file: File, altText: string) => Promise<void>;
  helpText?: string;
}

/**
 * A reusable image-upload control: file picker, required alt text, preview of what is currently
 * stored, and its own busy/error state — used by every AIDX form that has a single image
 * (researcher photo, news/event image). Replacing an existing image re-runs validation on the
 * server; this component never assumes a selected file is valid before the upload call returns.
 */
export function AidxImageUpload({ imageUrl, currentAltText, disabled, onUpload, helpText }: AidxImageUploadProps) {
  const [file, setFile] = React.useState<File | null>(null);
  const [altText, setAltText] = React.useState(currentAltText ?? "");
  const [uploading, setUploading] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);
  const [notice, setNotice] = React.useState<string | null>(null);
  const inputRef = React.useRef<HTMLInputElement>(null);

  const preview = React.useMemo(() => (file ? URL.createObjectURL(file) : null), [file]);
  React.useEffect(() => {
    return () => {
      if (preview) URL.revokeObjectURL(preview);
    };
  }, [preview]);

  async function submit() {
    if (!file) {
      setError("Choose an image file first.");
      return;
    }
    if (!altText.trim()) {
      setError("A short description of the image is required for screen-reader and accessibility support.");
      return;
    }
    setUploading(true);
    setError(null);
    setNotice(null);
    try {
      await onUpload(file, altText.trim());
      setNotice("Image uploaded.");
      setFile(null);
      if (inputRef.current) inputRef.current.value = "";
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : "The upload failed. Try again.");
    } finally {
      setUploading(false);
    }
  }

  const shown = preview ?? imageUrl;

  return (
    <Card className="space-y-4 p-5">
      <h2 className="text-h4 text-foreground">Image</h2>
      {helpText ? <p className="text-small text-muted-foreground">{helpText}</p> : null}

      {shown ? (
        // eslint-disable-next-line @next/next/no-img-element -- server-proxied AIDX media route, not a Next-optimizable static asset
        <img
          src={shown}
          alt={preview ? "Selected image preview" : currentAltText ?? ""}
          className="max-h-48 rounded-md border border-border object-contain"
        />
      ) : (
        <p className="text-small text-muted-foreground">No image uploaded yet.</p>
      )}

      {notice ? (
        <Alert tone="success" title="Done">
          {notice}
        </Alert>
      ) : null}
      {error ? (
        <Alert tone="error" title="Couldn't upload this image">
          {error}
        </Alert>
      ) : null}

      <FormField id="aidx-image-file" label="Choose a new image" hint="PNG or JPEG, up to 5 MB.">
        {(props) => (
          <Input
            {...props}
            ref={inputRef}
            type="file"
            accept="image/png,image/jpeg,image/webp"
            disabled={disabled || uploading}
            onChange={(event) => setFile(event.target.files?.[0] ?? null)}
          />
        )}
      </FormField>

      <FormField
        id="aidx-image-alt"
        label="Image description (alt text)"
        required
        hint="Describes the image for people using a screen reader. Required for every image."
      >
        {(props) => (
          <Input
            {...props}
            value={altText}
            disabled={disabled || uploading}
            onChange={(event) => setAltText(event.target.value)}
          />
        )}
      </FormField>

      <div className="flex justify-end">
        <Button type="button" onClick={() => void submit()} loading={uploading} loadingText="Uploading…" disabled={disabled}>
          Upload image
        </Button>
      </div>
    </Card>
  );
}
