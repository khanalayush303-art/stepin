"use client";

import * as React from "react";
import { useAuth } from "@clerk/nextjs";
import { Bookmark, BookmarkCheck } from "lucide-react";
import { Button } from "@/components/ui/button";
import { apiClient, formError } from "@/lib/auth/client";

interface SavedItem {
  contentType: string;
  contentId: string;
}

/**
 * A save/unsave toggle for a published Project or Publication. Only plain string props cross
 * into this client island from the server-rendered detail page — never a component/icon prop,
 * the exact mistake that caused the earlier RSC-serialization crash (digest 259500521).
 */
export function AidxSaveButton({ contentType, contentId }: { contentType: "project" | "publication"; contentId: string }) {
  const { isLoaded, isSignedIn, getToken } = useAuth();
  const [saved, setSaved] = React.useState<boolean | null>(null);
  const [pending, setPending] = React.useState(false);
  const [error, setError] = React.useState<string>();

  React.useEffect(() => {
    if (!isLoaded || !isSignedIn) {
      return;
    }

    let cancelled = false;
    (async () => {
      const token = await getToken();
      try {
        const items = await apiClient.get<SavedItem[]>("/api/v1/aidx/saved-items", token);
        if (!cancelled) {
          setSaved(items.some((i) => i.contentType.toLowerCase() === contentType && i.contentId === contentId));
        }
      } catch {
        if (!cancelled) {
          setSaved(false);
        }
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [isLoaded, isSignedIn, getToken, contentType, contentId]);

  async function toggle() {
    setPending(true);
    setError(undefined);
    try {
      const token = await getToken();
      if (saved) {
        await apiClient.delete(`/api/v1/aidx/${contentType}/${contentId}/saved`, token);
        setSaved(false);
      } else {
        await apiClient.post(`/api/v1/aidx/${contentType}/${contentId}/saved`, token);
        setSaved(true);
      }
    } catch (err) {
      setError(formError(err));
    } finally {
      setPending(false);
    }
  }

  if (!isLoaded) {
    return null;
  }

  if (!isSignedIn) {
    return (
      <Button variant="outline" size="sm" asChild>
        <a href="/sign-in">
          <Bookmark className="size-4" aria-hidden="true" />
          Sign in to save
        </a>
      </Button>
    );
  }

  return (
    <div className="flex flex-col gap-1">
      <Button variant="outline" size="sm" loading={pending || saved === null} onClick={toggle}>
        {saved ? <BookmarkCheck className="size-4" aria-hidden="true" /> : <Bookmark className="size-4" aria-hidden="true" />}
        {saved ? "Saved" : "Save"}
      </Button>
      {error ? <p className="text-caption text-error">{error}</p> : null}
    </div>
  );
}
