"use client";

import * as React from "react";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { useAuth } from "@clerk/nextjs";
import { SearchX } from "lucide-react";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { EmptyState } from "@/components/ui/empty-state";
import { Skeleton } from "@/components/ui/skeleton";
import { DashboardShell } from "@/components/dashboard/dashboard-shell";
import { describeAdminError, type AdminOpportunityError } from "@/lib/aidx/admin";
import { labels } from "@/lib/aidx/format";
import {
  deleteAdminPerson,
  getAdminPerson,
  updateAdminPerson,
  type AdminPersonDetail,
  type PersonInput,
} from "@/lib/aidx/people-cms";
import { ADMIN_NAV } from "../../../../_nav";
import { ConfirmDialog } from "../../../opportunities/_components/confirm-dialog";
import { PersonForm } from "../../_components/person-form";

function toInput(person: AdminPersonDetail): PersonInput {
  return {
    displayName: person.displayName,
    slug: person.slug,
    category: person.category,
    position: person.position,
    biography: person.biography,
    orcidUrl: person.orcidUrl,
    googleScholarUrl: person.googleScholarUrl,
    linkedInUrl: person.linkedInUrl,
    websiteUrl: person.websiteUrl,
    published: person.published,
  };
}

export default function EditAidxPersonPage() {
  const params = useParams<{ id: string }>();
  const id = params.id;
  const { getToken } = useAuth();
  const router = useRouter();

  const [loaded, setLoaded] = React.useState<AdminPersonDetail | null>(null);
  const [value, setValue] = React.useState<PersonInput | null>(null);
  const [loading, setLoading] = React.useState(true);
  const [loadError, setLoadError] = React.useState<AdminOpportunityError | null>(null);
  const [saving, setSaving] = React.useState(false);
  const [saveError, setSaveError] = React.useState<AdminOpportunityError | null>(null);
  const [notice, setNotice] = React.useState<string | null>(null);
  const [confirmDelete, setConfirmDelete] = React.useState(false);
  const [deleting, setDeleting] = React.useState(false);
  const [reloadKey, setReloadKey] = React.useState(0);

  React.useEffect(() => {
    let cancelled = false;
    async function load() {
      setLoading(true);
      setLoadError(null);
      try {
        const token = await getToken();
        const person = await getAdminPerson(token, id);
        if (cancelled) return;
        setLoaded(person);
        setValue(toInput(person));
      } catch (caught) {
        if (!cancelled) setLoadError(describeAdminError(caught));
      } finally {
        if (!cancelled) setLoading(false);
      }
    }
    void load();
    return () => {
      cancelled = true;
    };
  }, [id, getToken, reloadKey]);

  async function onSave() {
    if (!value) return;
    setSaving(true);
    setSaveError(null);
    setNotice(null);
    try {
      const token = await getToken();
      await updateAdminPerson(token, id, value);
      setNotice("Changes saved.");
      setReloadKey((key) => key + 1);
    } catch (caught) {
      setSaveError(describeAdminError(caught));
    } finally {
      setSaving(false);
    }
  }

  async function runDelete() {
    setDeleting(true);
    try {
      const token = await getToken();
      await deleteAdminPerson(token, id);
      router.push("/admin/aidx/people");
    } catch (caught) {
      setSaveError(describeAdminError(caught));
      setConfirmDelete(false);
      setDeleting(false);
    }
  }

  const fieldErrors = saveError?.kind === "validation" ? saveError.fields : {};
  const hasAuthorship = (loaded?.publications.length ?? 0) > 0;

  return (
    <DashboardShell nav={ADMIN_NAV} navLabel="Admin dashboard">
      <div className="max-w-3xl space-y-6">
        <nav aria-label="Breadcrumb" className="text-small text-muted-foreground">
          <Link href="/admin/aidx/people" className="underline-offset-4 hover:underline">
            Researchers and people
          </Link>
          <span aria-hidden="true"> / </span>
          <span className="text-foreground">Edit</span>
        </nav>

        {loading ? (
          <div className="space-y-4" aria-busy="true" aria-label="Loading researcher">
            <Skeleton className="h-8 w-2/3" />
            <Skeleton className="h-64 w-full" />
          </div>
        ) : null}

        {!loading && loadError?.kind === "not-found" ? (
          <EmptyState
            icon={<SearchX aria-hidden="true" className="size-6" />}
            title="Researcher not found"
            description={loadError.message}
            action={
              <Button asChild variant="outline">
                <Link href="/admin/aidx/people">Back to people</Link>
              </Button>
            }
          />
        ) : null}

        {!loading && loadError && loadError.kind !== "not-found" ? (
          <Alert tone={loadError.kind === "unauthenticated" ? "warning" : "error"} title="Unable to load this researcher">
            <p>{loadError.message}</p>
            {loadError.kind !== "forbidden" ? (
              <Button variant="outline" className="mt-3" onClick={() => setReloadKey((key) => key + 1)}>
                Try again
              </Button>
            ) : null}
          </Alert>
        ) : null}

        {!loading && loaded && value ? (
          <>
            <div className="space-y-1">
              <h1 className="break-words text-h2 text-foreground">{loaded.displayName}</h1>
              <p className="text-small text-muted-foreground">
                {labels.researcher(loaded.category)} · {loaded.published ? "Public" : "Hidden"} · /aidx/people/{loaded.slug}
              </p>
            </div>

            {notice ? (
              <Alert tone="success" title="Done">
                {notice}
              </Alert>
            ) : null}

            {saveError ? (
              <Alert tone="error" title={saveError.kind === "validation" ? "Check the form" : "Couldn't complete that change"}>
                {saveError.message}
              </Alert>
            ) : null}

            <Card className="space-y-3 p-5">
              <h2 className="text-h4 text-foreground">Projects</h2>
              {loaded.projects.length > 0 ? (
                <ul className="space-y-2 text-small">
                  {loaded.projects.map((project) => (
                    <li key={project.id}>
                      <Link
                        href={`/admin/aidx/projects/${project.id}/edit`}
                        className="font-medium text-primary underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
                      >
                        {project.title}
                      </Link>{" "}
                      <span className="text-muted-foreground">({project.status})</span>
                    </li>
                  ))}
                </ul>
              ) : (
                <p className="text-small text-muted-foreground">Not linked to any project.</p>
              )}
              <p className="text-caption text-muted-foreground">
                Project team links are changed from each project&rsquo;s page. They are read-only here.
              </p>
            </Card>

            <Card className="space-y-3 p-5">
              <h2 className="text-h4 text-foreground">Publications</h2>
              {loaded.publications.length > 0 ? (
                <ul className="space-y-1 text-small text-foreground-secondary">
                  {loaded.publications.map((publication) => (
                    <li key={publication.id} className="break-words">
                      {publication.title} <span className="text-muted-foreground">({publication.year})</span>
                    </li>
                  ))}
                </ul>
              ) : (
                <p className="text-small text-muted-foreground">Not an author on any publication.</p>
              )}
            </Card>

            <form
              onSubmit={(event) => {
                event.preventDefault();
                void onSave();
              }}
              className="space-y-6"
            >
              <PersonForm value={value} onChange={setValue} fieldErrors={fieldErrors} disabled={saving || deleting} />
              <div className="flex flex-wrap items-center justify-between gap-3">
                <div className="space-y-1">
                  <Button
                    type="button"
                    variant="outline"
                    onClick={() => setConfirmDelete(true)}
                    disabled={saving || deleting || hasAuthorship}
                    aria-describedby={hasAuthorship ? "delete-blocked" : undefined}
                  >
                    Delete researcher
                  </Button>
                  {hasAuthorship ? (
                    <p id="delete-blocked" className="text-caption text-muted-foreground">
                      This researcher cannot be deleted while they are an author on a publication.
                    </p>
                  ) : null}
                </div>
                <Button type="submit" size="lg" loading={saving} loadingText="Saving…" disabled={deleting}>
                  Save changes
                </Button>
              </div>
            </form>
          </>
        ) : null}
      </div>

      <ConfirmDialog
        open={confirmDelete}
        onOpenChange={(open) => !open && setConfirmDelete(false)}
        title="Delete researcher?"
        description={`"${loaded?.displayName ?? ""}" will be permanently removed, along with their team links on ${loaded?.projects.length ?? 0} project(s). This cannot be undone.`}
        confirmLabel="Delete researcher"
        destructive
        busy={deleting}
        onConfirm={() => void runDelete()}
      />
    </DashboardShell>
  );
}
