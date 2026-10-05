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
import { formatDateTime } from "@/lib/aidx/format";
import {
  archiveAdminEvent,
  deleteAdminEvent,
  getAdminEvent,
  publishAdminEvent,
  updateAdminEvent,
  type AdminEventDetail,
} from "@/lib/aidx/events-cms";
import { ADMIN_NAV } from "../../../../_nav";
import { ConfirmDialog } from "../../../opportunities/_components/confirm-dialog";
import { ProjectStatusBadge } from "../../../projects/_components/project-status-badge";
import { EventForm, type EventFormValue } from "../../_components/event-form";
import { fromDetail, toInput } from "../../_components/mapping";

type Pending = "publish" | "archive" | "delete" | null;

export default function EditAidxEventPage() {
  const params = useParams<{ id: string }>();
  const id = params.id;
  const router = useRouter();
  const { getToken } = useAuth();

  const [item, setItem] = React.useState<AdminEventDetail | null>(null);
  const [value, setValue] = React.useState<EventFormValue | null>(null);
  const [loading, setLoading] = React.useState(true);
  const [loadError, setLoadError] = React.useState<AdminOpportunityError | null>(null);
  const [saving, setSaving] = React.useState(false);
  const [saveError, setSaveError] = React.useState<AdminOpportunityError | null>(null);
  const [localErrors, setLocalErrors] = React.useState<Record<string, string[]>>({});
  const [notice, setNotice] = React.useState<string | null>(null);
  const [pending, setPending] = React.useState<Pending>(null);
  const [actionBusy, setActionBusy] = React.useState(false);
  const [reloadKey, setReloadKey] = React.useState(0);

  React.useEffect(() => {
    let cancelled = false;
    async function load() {
      setLoading(true);
      setLoadError(null);
      try {
        const token = await getToken();
        const event = await getAdminEvent(token, id);
        if (cancelled) return;
        setItem(event);
        setValue(fromDetail(event));
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
    const prepared = toInput(value);
    setLocalErrors(prepared.errors);
    if (Object.keys(prepared.errors).length > 0) return;

    setSaving(true);
    setSaveError(null);
    setNotice(null);
    try {
      const token = await getToken();
      await updateAdminEvent(token, id, prepared.input);
      setNotice("Changes saved.");
      setReloadKey((key) => key + 1);
    } catch (caught) {
      setSaveError(describeAdminError(caught));
    } finally {
      setSaving(false);
    }
  }

  async function runAction(kind: Exclude<Pending, null>) {
    setActionBusy(true);
    setSaveError(null);
    setNotice(null);
    try {
      const token = await getToken();
      if (kind === "delete") {
        await deleteAdminEvent(token, id);
        router.push("/admin/aidx/events");
        return;
      }
      if (kind === "publish") {
        await publishAdminEvent(token, id);
        setNotice("Published. The event is now on the public events page.");
      } else {
        await archiveAdminEvent(token, id);
        setNotice("Archived. The event is no longer public.");
      }
      setPending(null);
      setReloadKey((key) => key + 1);
    } catch (caught) {
      setSaveError(describeAdminError(caught));
      setPending(null);
    } finally {
      setActionBusy(false);
    }
  }

  const status = item?.status;
  const canPublish = status === "Draft" || status === "Archived";
  const canArchive = status === "Published";
  const canDelete = status === "Draft";
  const fieldErrors = {
    ...(saveError?.kind === "validation" ? saveError.fields : {}),
    ...localErrors,
  };

  return (
    <DashboardShell nav={ADMIN_NAV} navLabel="Admin dashboard">
      <div className="max-w-3xl space-y-6">
        <nav aria-label="Breadcrumb" className="text-small text-muted-foreground">
          <Link href="/admin/aidx/events" className="underline-offset-4 hover:underline">
            Events
          </Link>
          <span aria-hidden="true"> / </span>
          <span className="text-foreground">Edit</span>
        </nav>

        {loading ? (
          <div className="space-y-4" aria-busy="true" aria-label="Loading event">
            <Skeleton className="h-8 w-2/3" />
            <Skeleton className="h-64 w-full" />
          </div>
        ) : null}

        {!loading && loadError?.kind === "not-found" ? (
          <EmptyState
            icon={<SearchX aria-hidden="true" className="size-6" />}
            title="Event not found"
            description={loadError.message}
            action={
              <Button asChild variant="outline">
                <Link href="/admin/aidx/events">Back to events</Link>
              </Button>
            }
          />
        ) : null}

        {!loading && loadError && loadError.kind !== "not-found" ? (
          <Alert tone={loadError.kind === "unauthenticated" ? "warning" : "error"} title="Unable to load this event">
            <p>{loadError.message}</p>
            {loadError.kind !== "forbidden" ? (
              <Button variant="outline" className="mt-3" onClick={() => setReloadKey((key) => key + 1)}>
                Try again
              </Button>
            ) : null}
          </Alert>
        ) : null}

        {!loading && item && value ? (
          <>
            <div className="space-y-1">
              <h1 className="break-words text-h2 text-foreground">{item.title}</h1>
              <div className="flex flex-wrap items-center gap-3 text-small text-muted-foreground">
                <ProjectStatusBadge status={item.status} />
                <span>
                  <time dateTime={item.startsAt}>{formatDateTime(item.startsAt)}</time>
                </span>
                <span className="break-all">Public page: /aidx/events/{item.slug}</span>
              </div>
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

            <Card className="space-y-4 p-5">
              <h2 className="text-h4 text-foreground">Publishing</h2>
              <p className="text-small text-muted-foreground">
                {status === "Published"
                  ? "This event is public. Archiving it removes it from the events page, and its link stops working."
                  : status === "Archived"
                    ? "This event is archived and private. Publishing it again makes it public."
                    : "This event is a draft and is not public. Publish it when the details are ready."}
              </p>
              <div className="flex flex-wrap gap-3">
                {canPublish ? (
                  <Button onClick={() => setPending("publish")} disabled={actionBusy || saving}>
                    Publish
                  </Button>
                ) : null}
                {canArchive ? (
                  <Button variant="outline" onClick={() => setPending("archive")} disabled={actionBusy || saving}>
                    Archive
                  </Button>
                ) : null}
                {canDelete ? (
                  <Button variant="outline" onClick={() => setPending("delete")} disabled={actionBusy || saving}>
                    Delete draft
                  </Button>
                ) : null}
              </div>
              {!canDelete ? (
                <p className="text-caption text-muted-foreground">
                  Published and archived events cannot be deleted, because their links may already be shared. Archive
                  takes them down instead.
                </p>
              ) : null}
            </Card>

            <form
              onSubmit={(event) => {
                event.preventDefault();
                void onSave();
              }}
              className="space-y-6"
            >
              <EventForm value={value} onChange={setValue} fieldErrors={fieldErrors} disabled={saving || actionBusy} />
              <div className="flex justify-end">
                <Button type="submit" size="lg" loading={saving} loadingText="Saving…" disabled={actionBusy}>
                  Save changes
                </Button>
              </div>
            </form>
          </>
        ) : null}
      </div>

      <ConfirmDialog
        open={pending === "publish"}
        onOpenChange={(open) => !open && setPending(null)}
        title="Publish event?"
        description={`The event will appear on the AIDX Lab events page at /aidx/events/${item?.slug ?? ""}.`}
        confirmLabel="Publish"
        busy={actionBusy}
        onConfirm={() => void runAction("publish")}
      />
      <ConfirmDialog
        open={pending === "archive"}
        onOpenChange={(open) => !open && setPending(null)}
        title="Archive event?"
        description="It will be removed from the public events page, and its link will stop working. You can publish it again later."
        confirmLabel="Archive"
        busy={actionBusy}
        onConfirm={() => void runAction("archive")}
      />
      <ConfirmDialog
        open={pending === "delete"}
        onOpenChange={(open) => !open && setPending(null)}
        title="Delete draft event?"
        description="This permanently removes the draft. This cannot be undone."
        confirmLabel="Delete draft"
        destructive
        busy={actionBusy}
        onConfirm={() => void runAction("delete")}
      />
    </DashboardShell>
  );
}
