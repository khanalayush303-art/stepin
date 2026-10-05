"use client";

import * as React from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useAuth } from "@clerk/nextjs";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { DashboardShell } from "@/components/dashboard/dashboard-shell";
import { describeAdminError, type AdminOpportunityError } from "@/lib/aidx/admin";
import { createAdminNews, type NewsInput } from "@/lib/aidx/news-cms";
import { listAdminPeople, type AdminPersonSummary } from "@/lib/aidx/people-cms";
import { ADMIN_NAV } from "../../../_nav";
import { NewsForm } from "../_components/news-form";

const EMPTY: NewsInput = { title: "", slug: null, summary: "", body: "", authorResearcherId: null };

export default function NewAidxNewsPage() {
  const router = useRouter();
  const { getToken } = useAuth();
  const [value, setValue] = React.useState<NewsInput>(EMPTY);
  const [researchers, setResearchers] = React.useState<AdminPersonSummary[]>([]);
  const [saving, setSaving] = React.useState(false);
  const [error, setError] = React.useState<AdminOpportunityError | null>(null);

  React.useEffect(() => {
    let cancelled = false;
    getToken()
      .then((token) => listAdminPeople(token, { pageSize: 100 }))
      .then((page) => {
        if (!cancelled) setResearchers(page.items);
      })
      .catch(() => {
        if (!cancelled) setResearchers([]);
      });
    return () => {
      cancelled = true;
    };
  }, [getToken]);

  async function onSave() {
    setSaving(true);
    setError(null);
    try {
      const token = await getToken();
      const created = await createAdminNews(token, value);
      router.push(`/admin/aidx/news/${created.id}/edit`);
    } catch (caught) {
      // Values stay in the form, so a validation error does not cost the admin their draft.
      setError(describeAdminError(caught));
      setSaving(false);
    }
  }

  const fieldErrors = error?.kind === "validation" ? error.fields : {};

  return (
    <DashboardShell nav={ADMIN_NAV} navLabel="Admin dashboard">
      <div className="max-w-3xl space-y-6">
        <nav aria-label="Breadcrumb" className="text-small text-muted-foreground">
          <Link href="/admin/aidx/news" className="underline-offset-4 hover:underline">
            News
          </Link>
          <span aria-hidden="true"> / </span>
          <span className="text-foreground">New</span>
        </nav>

        <div className="space-y-1">
          <h1 className="text-h2 text-foreground">Write news</h1>
          <p className="text-body text-muted-foreground">Saved as a draft. It is not public until you publish it.</p>
        </div>

        {error ? (
          <Alert tone="error" title={error.kind === "validation" ? "Check the form" : "Couldn't save this story"}>
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
              <Link href="/admin/aidx/news">Cancel</Link>
            </Button>
            <Button type="submit" size="lg" loading={saving} loadingText="Saving…">
              Save draft
            </Button>
          </div>
        </form>
      </div>
    </DashboardShell>
  );
}
