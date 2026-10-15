"use client";

import * as React from "react";
import Link from "next/link";
import { Bookmark } from "lucide-react";
import { useAuth } from "@clerk/nextjs";
import { Alert } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { EmptyState } from "@/components/ui/empty-state";
import { Skeleton } from "@/components/ui/skeleton";
import { apiClient, formError } from "@/lib/auth/client";
import { formatDate } from "@/lib/utils";

interface SavedItem {
  id: string;
  createdAt: string;
  contentType: string;
  contentId: string;
  title: string;
  slug: string | null;
}

function detailHref(item: SavedItem): string {
  return item.contentType === "Project" ? `/aidx/projects/${item.slug}` : `/aidx/publications/${item.contentId}`;
}

/**
 * Any signed-in StepIn user's saved AIDX projects/publications — not gated behind AIDX lab
 * membership, since saving is open to every signed-in visitor, the same as saved jobs.
 */
export default function AidxSavedItemsPage() {
  const { isLoaded, isSignedIn, getToken } = useAuth();
  const [loading, setLoading] = React.useState(true);
  const [loadError, setLoadError] = React.useState<string>();
  const [items, setItems] = React.useState<SavedItem[]>([]);

  const load = React.useCallback(async () => {
    setLoading(true);
    setLoadError(undefined);
    try {
      const token = await getToken();
      setItems(await apiClient.get<SavedItem[]>("/api/v1/aidx/saved-items", token));
    } catch (error) {
      setLoadError(formError(error));
    } finally {
      setLoading(false);
    }
  }, [getToken]);

  React.useEffect(() => {
    if (!isLoaded || !isSignedIn) {
      return;
    }
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void load();
  }, [isLoaded, isSignedIn, load]);

  async function unsave(item: SavedItem) {
    try {
      const token = await getToken();
      await apiClient.delete(`/api/v1/aidx/${item.contentType.toLowerCase()}/${item.contentId}/saved`, token);
      setItems((current) => current.filter((i) => i.id !== item.id));
    } catch {
      // Non-fatal: the item stays in the list and the user can retry.
    }
  }

  if (!isLoaded) {
    return null;
  }

  if (!isSignedIn) {
    return (
      <div className="container-page max-w-2xl py-12">
        <Alert tone="info" title="Sign in to see your saved items">
          <Link href="/sign-in?returnTo=/aidx/saved" className="font-medium underline-offset-4 hover:underline">
            Sign in
          </Link>{" "}
          to view projects and publications you&rsquo;ve bookmarked.
        </Alert>
      </div>
    );
  }

  return (
    <div className="container-page max-w-3xl space-y-6 py-12">
      <div className="space-y-1">
        <h1 className="text-h2 text-foreground">Saved items</h1>
        <p className="text-body text-muted-foreground">AIDX projects and publications you&rsquo;ve bookmarked.</p>
      </div>

      {loading ? (
        <div className="space-y-3">
          <Skeleton className="h-16 w-full" />
          <Skeleton className="h-16 w-full" />
        </div>
      ) : loadError ? (
        <Alert tone="error" title="Couldn't load your saved items">
          {loadError}
          <div className="mt-3">
            <Button type="button" variant="outline" size="sm" onClick={() => void load()}>
              Try again
            </Button>
          </div>
        </Alert>
      ) : items.length === 0 ? (
        <EmptyState
          icon={<Bookmark className="size-6" />}
          title="No saved items yet"
          description="Save a project or publication while browsing to keep track of it here."
          action={
            <Button asChild>
              <Link href="/aidx/projects">Browse projects</Link>
            </Button>
          }
        />
      ) : (
        <Card className="p-6">
          <ul className="divide-y divide-border">
            {items.map((item) => (
              <li key={item.id} className="flex flex-wrap items-center gap-4 py-4 first:pt-0 last:pb-0">
                <div className="min-w-0 flex-1 space-y-1">
                  <div className="flex flex-wrap items-center gap-2">
                    <Link
                      href={detailHref(item)}
                      className="rounded-sm text-small font-medium text-foreground hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
                    >
                      {item.title}
                    </Link>
                    <Badge tone="neutral" variant="subtle">
                      {item.contentType}
                    </Badge>
                  </div>
                  <p className="text-caption text-muted-foreground">Saved {formatDate(item.createdAt)}</p>
                </div>
                <Button variant="outline" size="sm" onClick={() => void unsave(item)}>
                  Remove
                </Button>
              </li>
            ))}
          </ul>
        </Card>
      )}
    </div>
  );
}
