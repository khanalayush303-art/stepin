import { Alert } from "@/components/ui/alert";

export default function AccountDisabledPage() {
  return (
    <div className="space-y-5">
      <header className="space-y-1.5">
        <h1 className="text-h2 text-foreground">This account has been disabled</h1>
        <p className="text-body text-muted-foreground">
          You&apos;re signed in, but this account no longer has access to StepIn.
        </p>
      </header>

      <Alert tone="error" title="Access restricted">
        If you believe this is a mistake, contact support with the email address you use to sign in.
      </Alert>
    </div>
  );
}
