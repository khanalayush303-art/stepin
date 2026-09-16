"use client";

import * as React from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "@clerk/nextjs";
import { Button } from "@/components/ui/button";
import { FormField } from "@/components/ui/form-field";
import { Input } from "@/components/ui/input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Switch } from "@/components/ui/switch";
import { Alert } from "@/components/ui/alert";
import { completeAccountSetup } from "@/lib/auth/api";
import { formError } from "@/lib/auth/client";
import { cn } from "@/lib/utils";

const STEPS = ["Confirm your account type", "What you're after", "Notifications"];

export default function AccountSetupPage() {
  const router = useRouter();
  const { getToken } = useAuth();
  const [step, setStep] = React.useState(0);
  const [role, setRole] = React.useState<"Applicant" | "Recruiter">("Applicant");
  const [submitting, setSubmitting] = React.useState(false);
  const [serverError, setServerError] = React.useState<string>();

  async function onFinish() {
    setServerError(undefined);
    setSubmitting(true);
    try {
      const token = await getToken();
      const result = await completeAccountSetup(token, role);
      router.push(result.redirectTo);
    } catch (error) {
      setServerError(formError(error));
      setSubmitting(false);
    }
  }

  return (
    <div className="space-y-5">
      <header className="space-y-1.5">
        <p className="text-caption text-muted-foreground">
          Step {step + 1} of {STEPS.length}
        </p>
        <h1 className="text-h2 text-foreground">{STEPS[step]}</h1>
        <p className="text-body text-muted-foreground">
          This takes about two minutes and can be changed later.
        </p>
      </header>

      {/* Progress carries a text label as well as the bar. */}
      <div>
        <div
          className="flex gap-1.5"
          role="progressbar"
          aria-valuenow={step + 1}
          aria-valuemin={1}
          aria-valuemax={STEPS.length}
          aria-label={`Setup progress: step ${step + 1} of ${STEPS.length}`}
        >
          {STEPS.map((label, i) => (
            <span
              key={label}
              className={cn("h-1.5 flex-1 rounded-full", i <= step ? "bg-primary" : "bg-muted")}
            />
          ))}
        </div>
      </div>

      {serverError ? (
        <Alert tone="error" title="Couldn't save your account type">
          {serverError}
        </Alert>
      ) : null}

      <form onSubmit={(e) => e.preventDefault()} className="space-y-4">
        {step === 0 ? (
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
        ) : null}

        {step === 1 ? (
          <>
            <p className="rounded-md bg-muted px-3 py-2 text-caption text-muted-foreground">
              Job preferences arrive in a later phase — nothing on this step is saved yet.
            </p>
            <FormField id="work-type" label="What are you looking for?">
              {(props) => (
                <Select defaultValue="internship">
                  <SelectTrigger id={props.id}>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="internship">Internship or placement</SelectItem>
                    <SelectItem value="graduate">Graduate role</SelectItem>
                    <SelectItem value="part-time">Part-time while studying</SelectItem>
                  </SelectContent>
                </Select>
              )}
            </FormField>
            <FormField id="location" label="Preferred location">
              {(props) => <Input {...props} placeholder="Sydney, NSW" />}
            </FormField>
          </>
        ) : null}

        {step === 2 ? (
          <div className="space-y-4">
            <p className="rounded-md bg-muted px-3 py-2 text-caption text-muted-foreground">
              Notification preferences arrive in a later phase — nothing on this step is saved yet.
            </p>
            {[
              ["match", "Email me matching roles", "At most one email a day."],
              ["status", "Application status changes", "Sent as soon as an employer updates a stage."],
              ["digest", "Weekly digest", "A summary every Monday morning."],
            ].map(([id, label, hint]) => (
              <div key={id} className="flex items-start justify-between gap-4">
                <div>
                  <label htmlFor={id} className="text-small font-medium text-foreground">
                    {label}
                  </label>
                  <p className="text-caption text-muted-foreground">{hint}</p>
                </div>
                <Switch id={id} defaultChecked={id !== "digest"} />
              </div>
            ))}
          </div>
        ) : null}

        <div className="flex gap-3 pt-2">
          {step > 0 ? (
            <Button type="button" variant="outline" onClick={() => setStep((s) => s - 1)}>
              Back
            </Button>
          ) : null}
          {step === STEPS.length - 1 ? (
            <Button type="button" size="lg" className="flex-1" onClick={onFinish} loading={submitting} loadingText="Saving…">
              Finish setup
            </Button>
          ) : (
            <Button type="button" size="lg" className="flex-1" onClick={() => setStep((s) => Math.min(STEPS.length - 1, s + 1))}>
              Continue
            </Button>
          )}
        </div>
      </form>
    </div>
  );
}
