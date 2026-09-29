"use client";

import * as React from "react";
import { Building2, Clock, FileText, LayoutDashboard, Users } from "lucide-react";
import { useAuth } from "@clerk/nextjs";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { FormField } from "@/components/ui/form-field";
import { Input } from "@/components/ui/input";
import { Skeleton } from "@/components/ui/skeleton";
import { Textarea } from "@/components/ui/textarea";
import { DashboardShell, type DashboardNavItem } from "@/components/dashboard/dashboard-shell";
import { ApiError, formError } from "@/lib/auth/client";
import { getRecruiterProfile, updateRecruiterProfile } from "@/lib/profile/api";
import type { RecruiterProfile, UpdateRecruiterProfileInput } from "@/lib/profile/types";

const NAV: DashboardNavItem[] = [
  { href: "/recruiter", label: "Overview", icon: LayoutDashboard },
  { href: "/recruiter/jobs", label: "Job listings", icon: FileText, badge: "6" },
  { href: "/recruiter/candidates", label: "Candidates", icon: Users, badge: "84" },
  { href: "/recruiter/interviews", label: "Interviews", icon: Clock, badge: "5" },
  { href: "/recruiter/profile", label: "Company profile", icon: Building2 },
];

interface FormState {
  jobTitle: string;
  phoneNumber: string;
  photoUrl: string;
  companyName: string;
  companyDescription: string;
  companyWebsite: string;
  companyLogoUrl: string;
  companyIndustry: string;
  companyLocation: string;
}

function toFormState(profile: RecruiterProfile): FormState {
  return {
    jobTitle: profile.jobTitle ?? "",
    phoneNumber: profile.phoneNumber ?? "",
    photoUrl: profile.photoUrl ?? "",
    companyName: profile.company?.name ?? "",
    companyDescription: profile.company?.description ?? "",
    companyWebsite: profile.company?.website ?? "",
    companyLogoUrl: profile.company?.logoUrl ?? "",
    companyIndustry: profile.company?.industry ?? "",
    companyLocation: profile.company?.location ?? "",
  };
}

function toUpdateInput(form: FormState, existingCompanyId: string | null): UpdateRecruiterProfileInput {
  const hasCompanyDetails = form.companyName.trim().length > 0;

  return {
    jobTitle: form.jobTitle || null,
    phoneNumber: form.phoneNumber || null,
    photoUrl: form.photoUrl || null,
    company: hasCompanyDetails
      ? {
          id: existingCompanyId,
          name: form.companyName,
          description: form.companyDescription || null,
          website: form.companyWebsite || null,
          logoUrl: form.companyLogoUrl || null,
          industry: form.companyIndustry || null,
          location: form.companyLocation || null,
        }
      : null,
  };
}

export default function RecruiterProfilePage() {
  const { getToken } = useAuth();
  const [loading, setLoading] = React.useState(true);
  const [saving, setSaving] = React.useState(false);
  const [loadError, setLoadError] = React.useState<string>();
  const [serverError, setServerError] = React.useState<string>();
  const [fieldErrors, setFieldErrors] = React.useState<Record<string, string[]>>({});
  const [savedMessage, setSavedMessage] = React.useState(false);
  const [companyId, setCompanyId] = React.useState<string | null>(null);
  const [form, setForm] = React.useState<FormState | null>(null);

  const load = React.useCallback(async () => {
    setLoading(true);
    setLoadError(undefined);
    try {
      const token = await getToken();
      const profile = await getRecruiterProfile(token);
      setForm(toFormState(profile));
      setCompanyId(profile.company?.id ?? null);
    } catch (error) {
      setLoadError(formError(error));
    } finally {
      setLoading(false);
    }
  }, [getToken]);

  React.useEffect(() => {
    // See the matching comment in the candidate profile page: this is the
    // documented fetch-on-mount pattern, not an accidental cascade.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void load();
  }, [load]);

  async function onSave() {
    if (!form) return;

    setSaving(true);
    setServerError(undefined);
    setFieldErrors({});
    setSavedMessage(false);

    try {
      const token = await getToken();
      const updated = await updateRecruiterProfile(token, toUpdateInput(form, companyId));
      setForm(toFormState(updated));
      setCompanyId(updated.company?.id ?? null);
      setSavedMessage(true);
    } catch (error) {
      if (error instanceof ApiError && error.problem.errors) {
        setFieldErrors(error.problem.errors);
      }
      setServerError(formError(error));
    } finally {
      setSaving(false);
    }
  }

  function update<K extends keyof FormState>(key: K, value: FormState[K]) {
    setForm((prev) => (prev ? { ...prev, [key]: value } : prev));
  }

  return (
    <DashboardShell nav={NAV} navLabel="Recruiter dashboard">
      <div className="max-w-3xl space-y-6">
        <div className="space-y-1">
          <h1 className="text-h2 text-foreground">My profile</h1>
          <p className="text-body text-muted-foreground">
            Your contact details and company profile, shown to candidates on your job listings.
          </p>
        </div>

        {loading ? (
          <div className="space-y-4">
            <Skeleton className="h-64 w-full" />
            <Skeleton className="h-64 w-full" />
          </div>
        ) : loadError ? (
          <Alert tone="error" title="Couldn't load your profile">
            {loadError}
            <div className="mt-3">
              <Button type="button" variant="outline" size="sm" onClick={() => void load()}>
                Try again
              </Button>
            </div>
          </Alert>
        ) : form ? (
          <form
            onSubmit={(e) => {
              e.preventDefault();
              void onSave();
            }}
            className="space-y-6"
          >
            {serverError ? (
              <Alert tone="error" title="Couldn't save your profile">
                {serverError}
              </Alert>
            ) : null}

            {savedMessage ? (
              <Alert tone="success" title="Profile saved">
                Your changes are live.
              </Alert>
            ) : null}

            <Card className="space-y-4 p-6">
              <h2 className="text-h4 text-foreground">Your details</h2>
              <div className="grid gap-4 sm:grid-cols-2">
                <FormField id="jobTitle" label="Job title" error={fieldErrors.jobTitle?.[0]}>
                  {(props) => (
                    <Input
                      {...props}
                      value={form.jobTitle}
                      onChange={(e) => update("jobTitle", e.target.value)}
                      placeholder="Talent Partner"
                    />
                  )}
                </FormField>
                <FormField id="phoneNumber" label="Phone" error={fieldErrors.phoneNumber?.[0]}>
                  {(props) => (
                    <Input
                      {...props}
                      type="tel"
                      value={form.phoneNumber}
                      onChange={(e) => update("phoneNumber", e.target.value)}
                      placeholder="+61 400 000 000"
                    />
                  )}
                </FormField>
                <FormField id="photoUrl" label="Profile photo URL" error={fieldErrors.photoUrl?.[0]}>
                  {(props) => (
                    <Input
                      {...props}
                      type="url"
                      value={form.photoUrl}
                      onChange={(e) => update("photoUrl", e.target.value)}
                      placeholder="https://…"
                    />
                  )}
                </FormField>
              </div>
            </Card>

            <Card className="space-y-4 p-6">
              <h2 className="text-h4 text-foreground">Company</h2>
              <div className="grid gap-4 sm:grid-cols-2">
                <FormField
                  id="companyName"
                  label="Company name"
                  error={fieldErrors["company.name"]?.[0]}
                  className="sm:col-span-2"
                >
                  {(props) => (
                    <Input
                      {...props}
                      value={form.companyName}
                      onChange={(e) => update("companyName", e.target.value)}
                      placeholder="Acme Pty Ltd"
                    />
                  )}
                </FormField>
                <FormField id="companyIndustry" label="Industry">
                  {(props) => (
                    <Input
                      {...props}
                      value={form.companyIndustry}
                      onChange={(e) => update("companyIndustry", e.target.value)}
                    />
                  )}
                </FormField>
                <FormField id="companyLocation" label="Location">
                  {(props) => (
                    <Input
                      {...props}
                      value={form.companyLocation}
                      onChange={(e) => update("companyLocation", e.target.value)}
                    />
                  )}
                </FormField>
                <FormField id="companyWebsite" label="Website" error={fieldErrors["company.website"]?.[0]}>
                  {(props) => (
                    <Input
                      {...props}
                      type="url"
                      value={form.companyWebsite}
                      onChange={(e) => update("companyWebsite", e.target.value)}
                      placeholder="https://…"
                    />
                  )}
                </FormField>
                <FormField id="companyLogoUrl" label="Logo URL">
                  {(props) => (
                    <Input
                      {...props}
                      type="url"
                      value={form.companyLogoUrl}
                      onChange={(e) => update("companyLogoUrl", e.target.value)}
                      placeholder="https://…"
                    />
                  )}
                </FormField>
              </div>
              <FormField
                id="companyDescription"
                label="About the company"
                error={fieldErrors["company.description"]?.[0]}
              >
                {(props) => (
                  <Textarea
                    {...props}
                    value={form.companyDescription}
                    onChange={(e) => update("companyDescription", e.target.value)}
                    rows={4}
                  />
                )}
              </FormField>
            </Card>

            <div className="flex justify-end">
              <Button type="submit" size="lg" loading={saving} loadingText="Saving…">
                Save profile
              </Button>
            </div>
          </form>
        ) : null}
      </div>
    </DashboardShell>
  );
}
