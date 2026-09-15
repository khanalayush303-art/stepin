"use client";

import * as React from "react";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { FileText, CheckCircle2, XCircle } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Alert } from "@/components/ui/alert";
import { resendVerification, verifyEmail } from "@/lib/auth/api";
import { formError } from "@/lib/auth/client";

type Status = "checking" | "no-token" | "success" | "error";

function VerifyEmailContent() {
  const searchParams = useSearchParams();
  const email = searchParams.get("email") ?? "";
  const token = searchParams.get("token");

  const [status, setStatus] = React.useState<Status>(token ? "checking" : "no-token");
  const [resendState, setResendState] = React.useState<"idle" | "sending" | "sent">("idle");
  const [errorMessage, setErrorMessage] = React.useState<string>();

  React.useEffect(() => {
    if (!token || !email) {
      return;
    }

    verifyEmail(email, token)
      .then(() => setStatus("success"))
      .catch((error: unknown) => {
        setErrorMessage(formError(error));
        setStatus("error");
      });
  }, [email, token]);

  async function onResend() {
    if (!email) return;
    setResendState("sending");
    try {
      await resendVerification(email);
    } finally {
      setResendState("sent");
    }
  }

  if (status === "checking") {
    return (
      <Card className="flex flex-col items-center gap-4 p-8 text-center shadow-sm">
        <span className="flex size-16 items-center justify-center rounded-full bg-primary-subtle text-primary" aria-hidden="true">
          <FileText className="size-7" />
        </span>
        <h1 className="text-h2 text-foreground">Verifying your email…</h1>
      </Card>
    );
  }

  if (status === "success") {
    return (
      <Card className="flex flex-col items-center gap-4 p-8 text-center shadow-sm">
        <span className="flex size-16 items-center justify-center rounded-full bg-success-subtle text-success" aria-hidden="true">
          <CheckCircle2 className="size-7" />
        </span>
        <h1 className="text-h2 text-foreground">Email verified</h1>
        <p className="text-body text-muted-foreground">You&apos;re all set. Sign in to finish setting up your account.</p>
        <Button asChild size="lg" className="w-full">
          <Link href="/sign-in">Continue to sign in</Link>
        </Button>
      </Card>
    );
  }

  if (status === "error") {
    return (
      <Card className="flex flex-col items-center gap-4 p-8 text-center shadow-sm">
        <span className="flex size-16 items-center justify-center rounded-full bg-error-subtle text-error" aria-hidden="true">
          <XCircle className="size-7" />
        </span>
        <h1 className="text-h2 text-foreground">This link is invalid or has expired</h1>
        <p className="text-body text-muted-foreground">{errorMessage}</p>
        {email ? (
          <Button variant="outline" className="w-full" onClick={onResend} loading={resendState === "sending"}>
            {resendState === "sent" ? "New link sent" : "Resend the link"}
          </Button>
        ) : null}
        <Link
          href="/register"
          className="rounded-sm text-caption text-primary hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
        >
          Wrong address? Change your email
        </Link>
      </Card>
    );
  }

  return (
    <Card className="flex flex-col items-center gap-4 p-8 text-center shadow-sm">
      <span className="flex size-16 items-center justify-center rounded-full bg-primary-subtle text-primary" aria-hidden="true">
        <FileText className="size-7" />
      </span>
      <h1 className="text-h2 text-foreground">Check your email</h1>
      <p className="text-body text-muted-foreground">
        {email
          ? `We sent a verification link to ${email}. It expires in 24 hours.`
          : "We sent a verification link to your address. It expires in 24 hours."}
      </p>

      {resendState === "sent" ? (
        <Alert tone="success" title="Link sent" className="w-full text-left">
          Check your inbox for a new verification link.
        </Alert>
      ) : null}

      {email ? (
        <Button variant="outline" className="w-full" onClick={onResend} loading={resendState === "sending"}>
          Resend the link
        </Button>
      ) : null}
      <Link
        href="/register"
        className="rounded-sm text-caption text-primary hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
      >
        Wrong address? Change your email
      </Link>
    </Card>
  );
}

export default function VerifyEmailPage() {
  return (
    <div className="space-y-5">
      <React.Suspense fallback={null}>
        <VerifyEmailContent />
      </React.Suspense>
    </div>
  );
}
