"use client";

import * as React from "react";
import { useParams } from "next/navigation";
import { useAuth } from "@clerk/nextjs";
import { Users } from "lucide-react";
import { Alert } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Card } from "@/components/ui/card";
import { EmptyState } from "@/components/ui/empty-state";
import { Skeleton } from "@/components/ui/skeleton";
import { apiClient, formError } from "@/lib/auth/client";
import { formatDateTime } from "@/lib/aidx/format";

interface EventRegistration {
  id: string;
  createdAt: string;
  name: string;
  email: string;
  status: "Registered" | "Cancelled";
  hasUserId: boolean;
}

/** An event's registration list — visible to its owner or a lab-wide (unscoped) Editor/LabManager/
 * Admin, since events have no research-area scoping. Enforced server-side; this page just renders
 * what the API returns. Read-only: there is no export/CSV capability on the backend yet. */
export default function AidxEventRegistrationsPage() {
  const { eventId } = useParams<{ eventId: string }>();
  const { getToken } = useAuth();
  const [registrations, setRegistrations] = React.useState<EventRegistration[]>();
  const [loading, setLoading] = React.useState(true);
  const [loadError, setLoadError] = React.useState<string>();

  const load = React.useCallback(async () => {
    setLoading(true);
    setLoadError(undefined);
    try {
      const token = await getToken();
      setRegistrations(await apiClient.get<EventRegistration[]>(`/api/v1/aidx/events/${eventId}/registrations`, token));
    } catch (error) {
      setLoadError(formError(error));
    } finally {
      setLoading(false);
    }
  }, [getToken, eventId]);

  React.useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void load();
  }, [load]);

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
        <Alert tone="error" title="Couldn't load this event's registrations.">
          {loadError}
        </Alert>
      </div>
    );
  }

  const activeCount = registrations?.filter((r) => r.status === "Registered").length ?? 0;

  return (
    <div className="container-page max-w-3xl space-y-6 py-10">
      <div className="space-y-1">
        <h1 className="text-h2 text-foreground">Event registrations</h1>
        <p className="text-body text-muted-foreground">{activeCount} currently registered.</p>
      </div>

      {registrations && registrations.length > 0 ? (
        <Card className="divide-y divide-border p-0">
          {registrations.map((registration) => (
            <div key={registration.id} className="flex flex-wrap items-center justify-between gap-2 p-4">
              <div>
                <p className="text-small font-medium text-foreground">
                  {registration.name} <span className="text-muted-foreground">&lt;{registration.email}&gt;</span>
                </p>
                <p className="text-caption text-muted-foreground">
                  {formatDateTime(registration.createdAt)}
                  {registration.hasUserId ? " · signed-in user" : " · anonymous"}
                </p>
              </div>
              <Badge tone={registration.status === "Registered" ? "success" : "neutral"}>{registration.status}</Badge>
            </div>
          ))}
        </Card>
      ) : (
        <EmptyState icon={<Users className="size-6" />} title="No registrations yet" description="People who register through this event's public page will appear here." />
      )}
    </div>
  );
}
