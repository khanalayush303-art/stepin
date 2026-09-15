"use client";

import * as React from "react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { Check, X } from "lucide-react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Button } from "@/components/ui/button";
import { FormField } from "@/components/ui/form-field";
import { Input } from "@/components/ui/input";
import { Alert } from "@/components/ui/alert";
import { getPasswordPolicy, resetPassword } from "@/lib/auth/api";
import { ApiError, formError } from "@/lib/auth/client";
import type { PasswordPolicy } from "@/lib/auth/types";
import { cn } from "@/lib/utils";

const schema = z
  .object({
    password: z.string().min(1, "Enter a new password"),
    confirmPassword: z.string().min(1, "Confirm your new password"),
  })
  .refine((values) => values.password === values.confirmPassword, {
    message: "Both passwords need to match",
    path: ["confirmPassword"],
  });

type FormValues = z.infer<typeof schema>;

/** Server config is authoritative; this only renders what it reports. */
function rulesFor(policy: PasswordPolicy | undefined) {
  if (!policy) return [];
  const rules: { id: string; label: string; test: (value: string) => boolean }[] = [
    {
      id: "length",
      label: `At least ${policy.requiredLength} characters`,
      test: (v) => v.length >= policy.requiredLength,
    },
  ];
  if (policy.requireDigit) rules.push({ id: "digit", label: "Contains a number", test: (v) => /\d/.test(v) });
  if (policy.requireLowercase) rules.push({ id: "lower", label: "Contains a lowercase letter", test: (v) => /[a-z]/.test(v) });
  if (policy.requireUppercase) rules.push({ id: "upper", label: "Contains an uppercase letter", test: (v) => /[A-Z]/.test(v) });
  if (policy.requireNonAlphanumeric) {
    rules.push({ id: "symbol", label: "Contains a symbol", test: (v) => /[^a-zA-Z0-9]/.test(v) });
  }
  return rules;
}

function ResetPasswordForm() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const email = searchParams.get("email") ?? "";
  const token = searchParams.get("token") ?? "";

  const [policy, setPolicy] = React.useState<PasswordPolicy>();
  const [serverError, setServerError] = React.useState<string>();
  const [invalidLink, setInvalidLink] = React.useState(!email || !token);
  const [done, setDone] = React.useState(false);

  React.useEffect(() => {
    getPasswordPolicy()
      .then(setPolicy)
      .catch(() => setPolicy(undefined));
  }, []);

  const {
    register,
    handleSubmit,
    watch,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({ resolver: zodResolver(schema) });

  const password = watch("password") ?? "";
  const rules = rulesFor(policy);

  async function onSubmit(values: FormValues) {
    setServerError(undefined);
    try {
      await resetPassword(email, token, values.password, values.confirmPassword);
      setDone(true);
    } catch (error) {
      // A validation problem (e.g. the new password fails policy) is a form
      // error to fix and resubmit; anything else means the token itself is
      // bad — Identity's own InvalidToken case included.
      if (error instanceof ApiError && error.problem.errors) {
        setServerError(formError(error));
        return;
      }
      setInvalidLink(true);
    }
  }

  if (invalidLink) {
    return (
      <div className="space-y-5">
        <header className="space-y-1.5">
          <h1 className="text-h2 text-foreground">This link is invalid or has expired</h1>
          <p className="text-body text-muted-foreground">
            Password reset links expire after 1 hour and can only be used once.
          </p>
        </header>
        <Button asChild size="lg" className="w-full">
          <Link href="/forgot-password">Request a new link</Link>
        </Button>
      </div>
    );
  }

  if (done) {
    return (
      <div className="space-y-5">
        <Alert tone="success" title="Password updated">
          You can now sign in with your new password.
        </Alert>
        <Button size="lg" className="w-full" onClick={() => router.push("/sign-in")}>
          Back to sign in
        </Button>
      </div>
    );
  }

  return (
    <div className="space-y-5">
      <header className="space-y-1.5">
        <h1 className="text-h2 text-foreground">Choose a new password</h1>
        <p className="text-body text-muted-foreground">
          You will be signed out of other devices once this is saved.
        </p>
      </header>

      {serverError ? (
        <Alert tone="error" title="Couldn't update your password">
          {serverError}
        </Alert>
      ) : null}

      <form onSubmit={handleSubmit(onSubmit)} noValidate className="space-y-4">
        <FormField id="new-password" label="New password" required>
          {(props) => <Input {...props} {...register("password")} type="password" autoComplete="new-password" />}
        </FormField>

        {rules.length > 0 ? (
          <ul className="space-y-1.5" aria-label="Password requirements">
            {rules.map((rule) => {
              const ok = rule.test(password);
              return (
                <li key={rule.id} className="flex items-center gap-2 text-caption">
                  {ok ? (
                    <Check className="size-3.5 text-success" aria-hidden="true" />
                  ) : (
                    <X className="size-3.5 text-muted-foreground" aria-hidden="true" />
                  )}
                  <span className={cn(ok ? "text-success-subtle-fg" : "text-muted-foreground")}>{rule.label}</span>
                  <span className="sr-only">{ok ? "met" : "not met"}</span>
                </li>
              );
            })}
          </ul>
        ) : null}

        <FormField id="confirm-password" label="Confirm new password" error={errors.confirmPassword?.message} required>
          {(props) => (
            <Input {...props} {...register("confirmPassword")} type="password" autoComplete="new-password" />
          )}
        </FormField>

        <Button type="submit" size="lg" className="w-full" loading={isSubmitting} loadingText="Saving…">
          Save new password
        </Button>
      </form>
    </div>
  );
}

export default function ResetPasswordPage() {
  return (
    <React.Suspense fallback={null}>
      <ResetPasswordForm />
    </React.Suspense>
  );
}
