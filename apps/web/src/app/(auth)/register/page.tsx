"use client";

import * as React from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { zodResolver } from "@hookform/resolvers/zod";
import { Controller, useForm } from "react-hook-form";
import { z } from "zod";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { FormField } from "@/components/ui/form-field";
import { Input } from "@/components/ui/input";
import { Alert } from "@/components/ui/alert";
import { AuthDivider, GoogleButton } from "@/components/auth/google-button";
import { register as registerAccount } from "@/lib/auth/api";
import { fieldError, formError } from "@/lib/auth/client";
import { cn } from "@/lib/utils";

type Role = "Applicant" | "Recruiter";

const schema = z
  .object({
    firstName: z.string().min(1, "Enter your first name"),
    lastName: z.string().min(1, "Enter your last name"),
    email: z
      .string()
      .min(1, "Enter a complete email address, like name@example.com")
      .email("Enter a complete email address, like name@example.com"),
    password: z.string().min(8, "Use at least 8 characters"),
    confirmPassword: z.string().min(1, "Confirm your password"),
    agreed: z.boolean().refine((value) => value === true, {
      message: "You need to accept the terms before creating an account",
    }),
  })
  .refine((values) => values.password === values.confirmPassword, {
    message: "Both passwords need to match",
    path: ["confirmPassword"],
  });

type FormValues = z.infer<typeof schema>;

export default function RegisterPage() {
  const router = useRouter();
  const [role, setRole] = React.useState<Role>("Applicant");
  const [serverError, setServerError] = React.useState<string>();

  const {
    register,
    control,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({ resolver: zodResolver(schema), defaultValues: { agreed: false } });

  async function onSubmit(values: FormValues) {
    setServerError(undefined);

    try {
      await registerAccount({
        email: values.email,
        password: values.password,
        confirmPassword: values.confirmPassword,
        firstName: values.firstName,
        lastName: values.lastName,
        role,
      });
      router.push(`/verify-email?email=${encodeURIComponent(values.email)}`);
    } catch (error) {
      const emailMessage = fieldError(error, "email");
      if (emailMessage) {
        setError("email", { message: emailMessage });
        return;
      }
      setServerError(formError(error));
    }
  }

  return (
    <div className="space-y-5">
      <header className="space-y-1.5">
        <h1 className="text-h2 text-foreground">Create your account</h1>
        <p className="text-body text-muted-foreground">
          Free for students and graduates. No card required.
        </p>
      </header>

      <div role="radiogroup" aria-label="Account type" className="flex gap-1 rounded-md bg-muted p-1">
        {(
          [
            ["Applicant", "I am looking for work"],
            ["Recruiter", "I am hiring"],
          ] as const
        ).map(([value, label]) => (
          <button
            key={value}
            type="button"
            role="radio"
            aria-checked={role === value}
            onClick={() => setRole(value)}
            className={cn(
              "h-control-sm flex-1 rounded-sm text-small transition-colors",
              "focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring",
              role === value
                ? "bg-surface font-semibold text-foreground shadow-xs"
                : "text-muted-foreground hover:text-foreground"
            )}
          >
            {label}
          </button>
        ))}
      </div>

      <GoogleButton label="Sign up with Google" />
      <AuthDivider />

      {serverError ? (
        <Alert tone="error" title="Couldn't create your account">
          {serverError}
        </Alert>
      ) : null}

      <form onSubmit={handleSubmit(onSubmit)} noValidate className="space-y-4">
        <div className="grid grid-cols-2 gap-3">
          <FormField id="firstName" label="First name" error={errors.firstName?.message} required>
            {(props) => <Input {...props} {...register("firstName")} autoComplete="given-name" />}
          </FormField>
          <FormField id="lastName" label="Last name" error={errors.lastName?.message} required>
            {(props) => <Input {...props} {...register("lastName")} autoComplete="family-name" />}
          </FormField>
        </div>

        <FormField
          id="reg-email"
          label={role === "Applicant" ? "Email address" : "Work email address"}
          hint={
            role === "Applicant"
              ? "A university address gets you verified faster."
              : "Must be on your organisation's own domain."
          }
          error={errors.email?.message}
          required
        >
          {(props) => (
            <Input
              {...props}
              {...register("email")}
              type="email"
              autoComplete="email"
              placeholder={role === "Applicant" ? "you@university.edu.au" : "you@company.com.au"}
            />
          )}
        </FormField>

        <FormField
          id="reg-password"
          label="Create a password"
          hint="At least 8 characters, mixing case, a number and a symbol."
          error={errors.password?.message}
          required
        >
          {(props) => <Input {...props} {...register("password")} type="password" autoComplete="new-password" />}
        </FormField>

        <FormField id="reg-confirm-password" label="Confirm password" error={errors.confirmPassword?.message} required>
          {(props) => (
            <Input {...props} {...register("confirmPassword")} type="password" autoComplete="new-password" />
          )}
        </FormField>

        <div className="space-y-1.5">
          <div className="flex items-start gap-2.5">
            <Controller
              name="agreed"
              control={control}
              render={({ field }) => (
                <Checkbox
                  id="terms"
                  checked={field.value}
                  onCheckedChange={(value) => field.onChange(value === true)}
                  aria-describedby={errors.agreed ? "terms-error" : undefined}
                />
              )}
            />
            <label htmlFor="terms" className="text-small text-foreground-secondary">
              I agree to the Terms of Use and Privacy Policy.
            </label>
          </div>
          {errors.agreed ? (
            <p id="terms-error" role="alert" className="text-caption text-error">
              {errors.agreed.message}
            </p>
          ) : null}
        </div>

        <Button type="submit" size="lg" className="w-full" loading={isSubmitting} loadingText="Creating account…">
          Create account
        </Button>
      </form>

      <p className="text-center text-small text-muted-foreground">
        Already have an account?{" "}
        <Link href="/sign-in" className="font-medium text-primary hover:underline">
          Sign in
        </Link>
      </p>
    </div>
  );
}
