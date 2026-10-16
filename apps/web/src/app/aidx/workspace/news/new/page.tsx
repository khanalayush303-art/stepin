"use client";

import * as React from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useAuth } from "@clerk/nextjs";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { NewsForm } from "@/app/(app)/admin/aidx/news/_components/news-form";
import { describeAdminError, type AdminOpportunityError } from "@/lib/aidx/admin";
import { createAuthoringNews } from "@/lib/aidx/authoring";
import type { NewsInput } from "@/lib/aidx/news-cms";
import { listAdminPeople, type AdminPersonSummary } from "@/lib/aidx/people-cms";

const EMPTY: NewsInput = { title: "", slug: null, summary: "", body: "", authorResearcherId: null, expectedVersion: null };

export default function NewWorkspaceNewsPage() {
  const router = useRouter();
  const { getToken } = useAuth();
  const [value, setValue] = React.useState<NewsInput>(EMPTY);
  const [researchers, setResearchers] = React.useState<AdminPersonSummary[]>([]);
  const [saving, setSaving] = React.useState(false);
  const [error, setError] = React.useState<AdminOpportunityError | null>(null);

  React.useEffect(() => {
    void getToken().then((token) =>
      listAdminPeople(token, { pageSize: 200 }).then((p) => setResearchers(p.items)).catch(() => setResearchers([]))
    );
  }, [getToken]);

  async function onSave() {
    setSaving(true);
    setError(null);
    try {
      const token = await getToken();
      const created = await createAuthoringNews(token, value);
      router.push(`/aidx/workspace/news/${created.id}/edit`);
    } catch (caught) {
      setError(describeAdminError(caught));
      setSaving(false);
    }
  }

  const fieldErrors = error?.kind === "validation" ? error.fields : {};

  return (
    <div className="container-page max-w-3xl space-y-6 py-10">
      <nav aria-label="Breadcrumb" className="text-small text-muted-foreground">
        <Link href="/aidx/workspace" className="underline-offset-4 hover:underline">
          My AIDX workspace
        </Link>
        <span aria-hidden="true"> / </span>
        <span className="text-foreground">New story</span>
      </nav>

      <div className="space-y-1">
        <h1 className="text-h2 text-foreground">Write a news story</h1>
        <p className="text-body text-muted-foreground">Saved as your own draft until you submit it for review.</p>
      </div>

      {error ? (
        <Alert tone="error" title={error.kind === "validation" ? "Check the form" : "Couldn't create this story"}>
          {error.message}
        </Alert>
      ) : null}

      <form
        onSubmit={(event) => {
          event.preventDefault();
          void onSave();
        }}
        className="space-y-6"
      >
        <NewsForm value={value} onChange={setValue} fieldErrors={fieldErrors} disabled={saving} researchers={researchers} />
        <div className="flex justify-end gap-3">
          <Button variant="outline" asChild>
            <Link href="/aidx/workspace">Cancel</Link>
          </Button>
          <Button type="submit" size="lg" loading={saving} loadingText="Saving…">
            Save draft
          </Button>
        </div>
      </form>
    </div>
  );
}
