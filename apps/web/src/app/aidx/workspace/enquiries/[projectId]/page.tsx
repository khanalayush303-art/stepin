"use client";

import * as React from "react";
import { useParams } from "next/navigation";
import { useAuth } from "@clerk/nextjs";
import { Mail } from "lucide-react";
import { Alert } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { EmptyState } from "@/components/ui/empty-state";
import { Skeleton } from "@/components/ui/skeleton";
import { apiClient, formError } from "@/lib/auth/client";
import { formatDate } from "@/lib/utils";

interface Enquiry {
  id: string;
  createdAt: string;
  name: string;
  email: string;
  message: string;
  status: "New" | "Reviewed" | "Closed";
}

/** A project's private collaboration-enquiry inbox — visible to its owner, an in-scope Editor, or
 * LabManager/Admin (enforced server-side; this page just renders what the API returns). */
export default function AidxProjectEnquiriesPage() {
  const { projectId } = useParams<{ projectId: string }>();
  const { getToken } = useAuth();
  const [enquiries, setEnquiries] = React.useState<Enquiry[]>();
  const [loading, setLoading] = React.useState(true);
  const [loadError, setLoadError] = React.useState<string>();
  const [actingOn, setActingOn] = React.useState<string>();

  const load = React.useCallback(async () => {
    setLoading(true);
    setLoadError(undefined);
    try {
      const token = await getToken();
      setEnquiries(await apiClient.get<Enquiry[]>(`/api/v1/aidx/projects/${projectId}/enquiries`, token));
    } catch (error) {
      setLoadError(formError(error));
    } finally {
      setLoading(false);
    }
  }, [getToken, projectId]);

  React.useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void load();
  }, [load]);

  async function setStatus(enquiry: Enquiry, status: "Reviewed" | "Closed") {
    setActingOn(enquiry.id);
    try {
      const token = await getToken();
      await apiClient.put(`/api/v1/aidx/projects/${projectId}/enquiries/${enquiry.id}/status`, token, { status });
      setEnquiries((current) => current?.map((e) => (e.id === enquiry.id ? { ...e, status } : e)));
    } catch {
      // Left unchanged — the viewer can retry; a toast isn't in scope for this page.
    } finally {
      setActingOn(undefined);
    }
  }

  if (loading) {
    return (
      <div className="container-page space-y-4 py-10">
        <Skeleton className="h-10 w-64" />
        <Skeleton className="h-32 w-full" />
      </div>
    );
  }

  if (loadError) {
    return (
      <div className="container-page py-10">
        <Alert tone="error" title="Couldn't load this project's enquiries.">
          {loadError}
        </Alert>
      </div>
    );
  }

  return (
    <div className="container-page max-w-3xl space-y-6 py-10">
      <div className="space-y-1">
        <h1 className="text-h2 text-foreground">Collaboration enquiries</h1>
        <p className="text-body text-muted-foreground">Messages submitted through this project&rsquo;s public page.</p>
      </div>

      {enquiries && enquiries.length > 0 ? (
        <Card className="divide-y divide-border p-0">
          {enquiries.map((enquiry) => (
            <div key={enquiry.id} className="space-y-2 p-4">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <div>
                  <p className="text-small font-medium text-foreground">
                    {enquiry.name} <span className="text-muted-foreground">&lt;{enquiry.email}&gt;</span>
                  </p>
                  <p className="text-caption text-muted-foreground">{formatDate(enquiry.createdAt)}</p>
                </div>
                <Badge tone={enquiry.status === "New" ? "info" : enquiry.status === "Reviewed" ? "warning" : "neutral"}>
                  {enquiry.status}
                </Badge>
              </div>
              <p className="text-body text-foreground-secondary">{enquiry.message}</p>
              <div className="flex gap-2">
                {enquiry.status !== "Reviewed" ? (
                  <Button variant="outline" size="sm" loading={actingOn === enquiry.id} onClick={() => setStatus(enquiry, "Reviewed")}>
                    Mark reviewed
                  </Button>
                ) : null}
                {enquiry.status !== "Closed" ? (
                  <Button variant="ghost" size="sm" loading={actingOn === enquiry.id} onClick={() => setStatus(enquiry, "Closed")}>
                    Close
                  </Button>
                ) : null}
              </div>
            </div>
          ))}
        </Card>
      ) : (
        <EmptyState icon={<Mail className="size-6" />} title="No enquiries yet" description="Messages from the public project page will appear here." />
      )}
    </div>
  );
}
