"use client";

import * as React from "react";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { ApiError } from "@/lib/auth/client";

/** Public, anonymous collaboration-enquiry form for one project. No real email delivery is
 * configured for this feature — the success state says exactly that, honestly. */
export function AidxCollaborationForm({ projectId }: { projectId: string }) {
  const [name, setName] = React.useState("");
  const [email, setEmail] = React.useState("");
  const [message, setMessage] = React.useState("");
  const [submitting, setSubmitting] = React.useState(false);
  const [submitted, setSubmitted] = React.useState(false);
  const [error, setError] = React.useState<string>();

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault();
    setSubmitting(true);
    setError(undefined);
    try {
      const response = await fetch(`/api/v1/aidx/projects/${projectId}/enquiries`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ name, email, message }),
      });
      if (!response.ok) {
        const problem = await response.json().catch(() => ({}));
        throw new ApiError(response.status, problem);
      }
      setSubmitted(true);
    } catch (err) {
      setError(err instanceof ApiError ? (err.problem.detail ?? err.problem.title ?? "Something went wrong.") : "We couldn't reach the server. Try again.");
    } finally {
      setSubmitting(false);
    }
  }

  if (submitted) {
    return (
      <Alert tone="success" title="Enquiry sent">
        The project team can see your message. No automatic email confirmation is sent — this page
        confirming receipt is the only acknowledgement.
      </Alert>
    );
  }

  return (
    <form onSubmit={onSubmit} className="space-y-3">
      <Input placeholder="Your name" value={name} onChange={(e) => setName(e.target.value)} required maxLength={200} />
      <Input type="email" placeholder="Your email" value={email} onChange={(e) => setEmail(e.target.value)} required maxLength={320} />
      <Textarea
        placeholder="What would you like to collaborate on?"
        value={message}
        onChange={(e) => setMessage(e.target.value)}
        required
        maxLength={5000}
        className="min-h-[6rem]"
      />
      {error ? <Alert tone="error" title="Couldn't send that.">{error}</Alert> : null}
      <Button type="submit" loading={submitting}>
        Send enquiry
      </Button>
    </form>
  );
}
