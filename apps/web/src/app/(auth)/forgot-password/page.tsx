"use client";

import * as React from "react";
import Link from "next/link";
import { ArrowLeft } from "lucide-react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Button } from "@/components/ui/button";
import { FormField } from "@/components/ui/form-field";
import { Input } from "@/components/ui/input";
import { Alert } from "@/components/ui/alert";
import { forgotPassword } from "@/lib/auth/api";
import { formError } from "@/lib/auth/client";

const schema = z.object({
  email: z
    .string()
    .min(1, "Enter a complete email address, like name@example.com")
    .email("Enter a complete email address, like name@example.com"),
});

type FormValues = z.infer<typeof schema>;

export default function ForgotPasswordPage() {
  const [sentTo, setSentTo] = React.useState<string>();
  const [serverError, setServerError] = React.useState<string>();

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({ resolver: zodResolver(schema) });

  async function onSubmit(values: FormValues) {
    setServerError(undefined);
    try {
      await forgotPassword(values.email);
      setSentTo(values.email);
    } catch (error) {
      setServerError(formError(error));
    }
  }

  return (
    <div className="space-y-5">
      <header className="space-y-1.5">
        <h1 className="text-h2 text-foreground">Reset your password</h1>
        <p className="text-body text-muted-foreground">
          Enter the address you signed up with and we will send a reset link.
        </p>
      </header>

      {sentTo ? (
        <Alert tone="success" title="Check your inbox">
          If an account exists for {sentTo}, you will receive password reset instructions. The link
          expires in 1 hour.
        </Alert>
      ) : null}

      {serverError ? (
        <Alert tone="error" title="Something went wrong">
          {serverError}
        </Alert>
      ) : null}

      <form onSubmit={handleSubmit(onSubmit)} noValidate className="space-y-4">
        <FormField
          id="forgot-email"
          label="Email address"
          hint="We never reveal whether an address has an account."
          error={errors.email?.message}
          required
        >
          {(props) => <Input {...props} {...register("email")} type="email" autoComplete="email" />}
        </FormField>

        <Button type="submit" size="lg" className="w-full" loading={isSubmitting} loadingText="Sending…">
          Send reset link
        </Button>
      </form>

      <Link
        href="/sign-in"
        className="inline-flex items-center gap-2 rounded-sm text-small text-primary hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
      >
        <ArrowLeft className="size-4" aria-hidden="true" />
        Back to sign in
      </Link>
    </div>
  );
}
