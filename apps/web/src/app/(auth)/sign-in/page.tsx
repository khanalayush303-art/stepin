"use client";

import * as React from "react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { zodResolver } from "@hookform/resolvers/zod";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { FormField } from "@/components/ui/form-field";
import { Input } from "@/components/ui/input";
import { Alert } from "@/components/ui/alert";
import { AuthDivider, GoogleButton } from "@/components/auth/google-button";
import { useSession } from "@/lib/auth/session-context";
import { login } from "@/lib/auth/api";
import { ApiError, formError } from "@/lib/auth/client";
import { dashboardPathForRole } from "@/lib/auth/types";

const EMAIL_MESSAGE = "Enter a complete email address, like name@example.com";

const schema = z.object({
  email: z.string().min(1, EMAIL_MESSAGE).email(EMAIL_MESSAGE),
  password: z.string().min(1, "Enter your password"),
});

type FormValues = z.infer<typeof schema>;

function SignInForm() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const { refresh } = useSession();
  const [serverError, setServerError] = React.useState<string>();
  const [unverifiedEmail, setUnverifiedEmail] = React.useState<string>();
  const [rememberMe, setRememberMe] = React.useState(true);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({ resolver: zodResolver(schema) });

  async function onSubmit(values: FormValues) {
    setServerError(undefined);
    setUnverifiedEmail(undefined);

    try {
      const user = await login(values.email, values.password, rememberMe);
      await refresh();
      const returnTo = searchParams.get("returnTo");
      router.push(returnTo && returnTo.startsWith("/") ? returnTo : dashboardPathForRole(user.role));
    } catch (error) {
      if (error instanceof ApiError && error.problem.reason === "email_not_confirmed") {
        setUnverifiedEmail(values.email);
        return;
      }
      setServerError(formError(error));
    }
  }

  return (
    <div className="space-y-5">
      <header className="space-y-1.5">
        <h1 className="text-h2 text-foreground">Welcome back</h1>
        <p className="text-body text-muted-foreground">
          Sign in to pick up where you left off.
        </p>
      </header>

      <GoogleButton label="Continue with Google" />
      <AuthDivider />

      {serverError ? (
        <Alert tone="error" title="Couldn't sign you in">
          {serverError}
        </Alert>
      ) : null}

      {unverifiedEmail ? (
        <Alert tone="warning" title="Verify your email first">
          We sent a verification link to {unverifiedEmail}. Check your inbox, or{" "}
          <Link
            href={`/verify-email?email=${encodeURIComponent(unverifiedEmail)}`}
            className="font-medium underline"
          >
            resend it
          </Link>
          .
        </Alert>
      ) : null}

      <form onSubmit={handleSubmit(onSubmit)} noValidate className="space-y-4">
        <FormField id="email" label="Email address" error={errors.email?.message} required>
          {(props) => (
            <Input
              {...props}
              {...register("email")}
              type="email"
              autoComplete="email"
              placeholder="you@example.com"
            />
          )}
        </FormField>

        <FormField
          id="password"
          label="Password"
          error={errors.password?.message}
          required
          action={
            <Link
              href="/forgot-password"
              className="rounded-sm text-caption text-primary hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
            >
              Forgot password?
            </Link>
          }
        >
          {(props) => <Input {...props} {...register("password")} type="password" autoComplete="current-password" />}
        </FormField>

        <div className="flex items-center gap-2.5">
          <Checkbox id="remember" checked={rememberMe} onCheckedChange={(v) => setRememberMe(v === true)} />
          <label htmlFor="remember" className="text-small text-foreground-secondary">
            Keep me signed in on this device
          </label>
        </div>

        <Button type="submit" size="lg" className="w-full" loading={isSubmitting} loadingText="Signing in…">
          Sign in
        </Button>
      </form>

      <p className="text-center text-small text-muted-foreground">
        New to StepIn?{" "}
        <Link href="/register" className="font-medium text-primary hover:underline">
          Create an account
        </Link>
      </p>
    </div>
  );
}

export default function SignInPage() {
  return (
    <React.Suspense fallback={null}>
      <SignInForm />
    </React.Suspense>
  );
}
