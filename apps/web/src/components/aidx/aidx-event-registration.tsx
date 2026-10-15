"use client";

import * as React from "react";
import { useAuth } from "@clerk/nextjs";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { apiClient, formError } from "@/lib/auth/client";

interface Summary {
  registeredCount: number;
  capacity: number | null;
  isFull: boolean;
}

interface RegistrationResponse {
  id: string;
  status: string;
  cancellationToken: string | null;
}

/** Event registration — signed-in (one click, ownership is the credential) or anonymous (name +
 * email, a cancellation token shown once). Capacity/full state is read from the public summary
 * endpoint; attendee names are never fetched or shown here — organizers see those separately,
 * through the authenticated manage surface, never publicly. */
export function AidxEventRegistration({ eventId }: { eventId: string }) {
  const { isLoaded, isSignedIn, getToken } = useAuth();
  const [summary, setSummary] = React.useState<Summary>();
  const [alreadyRegistered, setAlreadyRegistered] = React.useState(false);
  const [name, setName] = React.useState("");
  const [email, setEmail] = React.useState("");
  const [submitting, setSubmitting] = React.useState(false);
  const [error, setError] = React.useState<string>();
  const [anonymousToken, setAnonymousToken] = React.useState<string>();

  const loadSummary = React.useCallback(async () => {
    try {
      const result = await apiClient.get<Summary>(`/api/v1/aidx/events/${eventId}/registrations/summary`, null);
      setSummary(result);
    } catch {
      // Silently unavailable — the registration form below still works; it just won't show a count.
    }
  }, [eventId]);

  React.useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void loadSummary();
  }, [loadSummary]);

  React.useEffect(() => {
    if (!isLoaded || !isSignedIn) {
      return;
    }
    let cancelled = false;
    (async () => {
      const token = await getToken();
      try {
        const mine = await apiClient.get<{ registered: boolean }>(`/api/v1/aidx/events/${eventId}/registrations/mine`, token);
        if (!cancelled) {
          setAlreadyRegistered(mine.registered);
        }
      } catch {
        // Treated as not registered — a non-blocking, informational check only.
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [isLoaded, isSignedIn, getToken, eventId]);

  async function register(e?: React.FormEvent) {
    e?.preventDefault();
    setSubmitting(true);
    setError(undefined);
    try {
      const token = isSignedIn ? await getToken() : null;
      const response = await apiClient.post<RegistrationResponse>(
        `/api/v1/aidx/events/${eventId}/registrations`,
        token,
        isSignedIn ? undefined : { name, email }
      );
      if (response.cancellationToken) {
        setAnonymousToken(response.cancellationToken);
      }
      setAlreadyRegistered(true);
      await loadSummary();
    } catch (err) {
      setError(formError(err));
    } finally {
      setSubmitting(false);
    }
  }

  if (!isLoaded) {
    return null;
  }

  if (anonymousToken) {
    return (
      <Alert tone="success" title="You're registered">
        Save this cancellation code if you need to cancel later — it is shown only once and is not
        emailed to you (no email delivery is configured for this feature):
        <code className="mt-2 block break-all rounded bg-muted p-2 text-caption">{anonymousToken}</code>
      </Alert>
    );
  }

  if (alreadyRegistered) {
    return <Alert tone="success" title="You're registered for this event." />;
  }

  if (summary?.isFull) {
    return <Alert tone="warning" title="This event is full." />;
  }

  if (isSignedIn) {
    return (
      <div className="space-y-2">
        {summary ? (
          <p className="text-caption text-muted-foreground">
            {summary.registeredCount} registered{summary.capacity ? ` of ${summary.capacity} places` : ""}
          </p>
        ) : null}
        {error ? <Alert tone="error" title="Registration failed.">{error}</Alert> : null}
        <Button onClick={() => register()} loading={submitting}>
          Register
        </Button>
      </div>
    );
  }

  return (
    <form onSubmit={register} className="space-y-3">
      {summary ? (
        <p className="text-caption text-muted-foreground">
          {summary.registeredCount} registered{summary.capacity ? ` of ${summary.capacity} places` : ""}
        </p>
      ) : null}
      <Input placeholder="Your name" value={name} onChange={(e) => setName(e.target.value)} required maxLength={200} />
      <Input type="email" placeholder="Your email" value={email} onChange={(e) => setEmail(e.target.value)} required maxLength={320} />
      {error ? <Alert tone="error" title="Registration failed.">{error}</Alert> : null}
      <Button type="submit" loading={submitting}>
        Register
      </Button>
    </form>
  );
}
