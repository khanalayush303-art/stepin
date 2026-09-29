"use client";

import * as React from "react";
import {
  Bell,
  Bookmark,
  FileText,
  GraduationCap,
  LayoutDashboard,
  Plus,
  Search,
  Trash2,
  User,
} from "lucide-react";
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
import { getCandidateProfile, updateCandidateProfile } from "@/lib/profile/api";
import type {
  CandidateCertification,
  CandidateEducation,
  CandidateExperience,
  CandidateProfile,
  UpdateCandidateProfileInput,
} from "@/lib/profile/types";

const NAV: DashboardNavItem[] = [
  { href: "/dashboard", label: "Overview", icon: LayoutDashboard },
  { href: "/dashboard/discover", label: "Discover roles", icon: Search },
  { href: "/dashboard/applications", label: "Applications", icon: FileText, badge: "12" },
  { href: "/dashboard/saved", label: "Saved roles", icon: Bookmark, badge: "8" },
  { href: "/dashboard/interviews", label: "Interviews", icon: User, badge: "2" },
  { href: "/dashboard/prepare", label: "Prepare", icon: GraduationCap },
  { href: "/dashboard/notifications", label: "Notifications", icon: Bell, badge: "3" },
  { href: "/dashboard/profile", label: "My profile", icon: User },
];

interface FormState {
  phoneNumber: string;
  location: string;
  headline: string;
  bio: string;
  photoUrl: string;
  linkedInUrl: string;
  portfolioUrl: string;
  gitHubUrl: string;
  skillsText: string;
  education: CandidateEducation[];
  experience: CandidateExperience[];
  certifications: CandidateCertification[];
}

function toFormState(profile: CandidateProfile): FormState {
  return {
    phoneNumber: profile.phoneNumber ?? "",
    location: profile.location ?? "",
    headline: profile.headline ?? "",
    bio: profile.bio ?? "",
    photoUrl: profile.photoUrl ?? "",
    linkedInUrl: profile.linkedInUrl ?? "",
    portfolioUrl: profile.portfolioUrl ?? "",
    gitHubUrl: profile.gitHubUrl ?? "",
    skillsText: profile.skills.join(", "),
    education: profile.education,
    experience: profile.experience,
    certifications: profile.certifications,
  };
}

function toUpdateInput(form: FormState): UpdateCandidateProfileInput {
  return {
    phoneNumber: form.phoneNumber || null,
    location: form.location || null,
    headline: form.headline || null,
    bio: form.bio || null,
    photoUrl: form.photoUrl || null,
    linkedInUrl: form.linkedInUrl || null,
    portfolioUrl: form.portfolioUrl || null,
    gitHubUrl: form.gitHubUrl || null,
    skills: form.skillsText
      .split(",")
      .map((s) => s.trim())
      .filter((s) => s.length > 0),
    education: form.education,
    experience: form.experience,
    certifications: form.certifications,
  };
}

export default function CandidateProfilePage() {
  const { getToken } = useAuth();
  const [loading, setLoading] = React.useState(true);
  const [saving, setSaving] = React.useState(false);
  const [loadError, setLoadError] = React.useState<string>();
  const [serverError, setServerError] = React.useState<string>();
  const [fieldErrors, setFieldErrors] = React.useState<Record<string, string[]>>({});
  const [savedMessage, setSavedMessage] = React.useState(false);
  const [completion, setCompletion] = React.useState(0);
  const [form, setForm] = React.useState<FormState | null>(null);

  const load = React.useCallback(async () => {
    setLoading(true);
    setLoadError(undefined);
    try {
      const token = await getToken();
      const profile = await getCandidateProfile(token);
      setForm(toFormState(profile));
      setCompletion(profile.profileCompletionPercent);
    } catch (error) {
      setLoadError(formError(error));
    } finally {
      setLoading(false);
    }
  }, [getToken]);

  React.useEffect(() => {
    // Fetch-on-mount: the initial profile load has no external event to key
    // off, so an effect is the correct tool (react.dev/learn/synchronizing-with-effects#fetching-data)
    // even though it — like any fetch-on-mount effect — causes the one
    // additional render that react-hooks/set-state-in-effect warns about.
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
      const updated = await updateCandidateProfile(token, toUpdateInput(form));
      setForm(toFormState(updated));
      setCompletion(updated.profileCompletionPercent);
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
    <DashboardShell nav={NAV} navLabel="Applicant dashboard">
      <div className="max-w-3xl space-y-6">
        <div className="space-y-1">
          <h1 className="text-h2 text-foreground">My profile</h1>
          <p className="text-body text-muted-foreground">
            This is what employers see when you apply. Keep it up to date.
          </p>
        </div>

        {loading ? (
          <div className="space-y-4">
            <Skeleton className="h-24 w-full" />
            <Skeleton className="h-64 w-full" />
            <Skeleton className="h-48 w-full" />
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
            <Card className="border-0 bg-primary-subtle p-6">
              <div className="space-y-2.5">
                <h2 className="text-h4 text-primary-subtle-fg">Your profile is {completion}% complete</h2>
                <div
                  className="h-2 w-full overflow-hidden rounded-full bg-surface"
                  role="progressbar"
                  aria-valuenow={completion}
                  aria-valuemin={0}
                  aria-valuemax={100}
                  aria-label="Profile completion"
                >
                  <span className="block h-full rounded-full bg-primary" style={{ width: `${completion}%` }} />
                </div>
              </div>
            </Card>

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
              <h2 className="text-h4 text-foreground">Basics</h2>
              <div className="grid gap-4 sm:grid-cols-2">
                <FormField id="headline" label="Professional headline" error={fieldErrors.headline?.[0]}>
                  {(props) => (
                    <Input
                      {...props}
                      value={form.headline}
                      onChange={(e) => update("headline", e.target.value)}
                      placeholder="Graduate Software Engineer"
                    />
                  )}
                </FormField>
                <FormField id="location" label="Location" error={fieldErrors.location?.[0]}>
                  {(props) => (
                    <Input
                      {...props}
                      value={form.location}
                      onChange={(e) => update("location", e.target.value)}
                      placeholder="Sydney, NSW"
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
              <FormField id="bio" label="About" hint="A few sentences about you." error={fieldErrors.bio?.[0]}>
                {(props) => (
                  <Textarea
                    {...props}
                    value={form.bio}
                    onChange={(e) => update("bio", e.target.value)}
                    rows={4}
                  />
                )}
              </FormField>
              <FormField
                id="skills"
                label="Skills"
                hint="Separate with commas."
                error={fieldErrors.skills?.[0]}
              >
                {(props) => (
                  <Input
                    {...props}
                    value={form.skillsText}
                    onChange={(e) => update("skillsText", e.target.value)}
                    placeholder="C#, TypeScript, SQL"
                  />
                )}
              </FormField>
            </Card>

            <Card className="space-y-4 p-6">
              <h2 className="text-h4 text-foreground">Links</h2>
              <div className="grid gap-4 sm:grid-cols-3">
                <FormField id="linkedInUrl" label="LinkedIn" error={fieldErrors.linkedInUrl?.[0]}>
                  {(props) => (
                    <Input
                      {...props}
                      type="url"
                      value={form.linkedInUrl}
                      onChange={(e) => update("linkedInUrl", e.target.value)}
                      placeholder="https://linkedin.com/in/…"
                    />
                  )}
                </FormField>
                <FormField id="portfolioUrl" label="Portfolio" error={fieldErrors.portfolioUrl?.[0]}>
                  {(props) => (
                    <Input
                      {...props}
                      type="url"
                      value={form.portfolioUrl}
                      onChange={(e) => update("portfolioUrl", e.target.value)}
                      placeholder="https://…"
                    />
                  )}
                </FormField>
                <FormField id="gitHubUrl" label="GitHub" error={fieldErrors.gitHubUrl?.[0]}>
                  {(props) => (
                    <Input
                      {...props}
                      type="url"
                      value={form.gitHubUrl}
                      onChange={(e) => update("gitHubUrl", e.target.value)}
                      placeholder="https://github.com/…"
                    />
                  )}
                </FormField>
              </div>
            </Card>

            <EducationSection
              items={form.education}
              error={fieldErrors.education?.[0]}
              onChange={(items) => update("education", items)}
            />

            <ExperienceSection
              items={form.experience}
              error={fieldErrors.experience?.[0]}
              onChange={(items) => update("experience", items)}
            />

            <CertificationsSection
              items={form.certifications}
              error={fieldErrors.certifications?.[0]}
              onChange={(items) => update("certifications", items)}
            />

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

function EducationSection({
  items,
  error,
  onChange,
}: {
  items: CandidateEducation[];
  error?: string;
  onChange: (items: CandidateEducation[]) => void;
}) {
  return (
    <Card className="space-y-4 p-6">
      <div className="flex items-center justify-between gap-3">
        <h2 className="text-h4 text-foreground">Education</h2>
        <Button
          type="button"
          variant="outline"
          size="sm"
          onClick={() =>
            onChange([
              ...items,
              { id: null, institution: "", degree: null, fieldOfStudy: null, startDate: null, endDate: null, description: null },
            ])
          }
        >
          <Plus /> Add education
        </Button>
      </div>
      {error ? <p className="text-small text-error">{error}</p> : null}
      {items.length === 0 ? (
        <p className="text-small text-muted-foreground">No education added yet.</p>
      ) : (
        <ul className="space-y-4">
          {items.map((item, i) => (
            <li key={item.id ?? i} className="space-y-3 rounded-md border border-border p-4">
              <div className="flex items-start justify-between gap-3">
                <div className="grid flex-1 gap-3 sm:grid-cols-2">
                  <FormField id={`edu-institution-${i}`} label="Institution">
                    {(props) => (
                      <Input
                        {...props}
                        value={item.institution}
                        onChange={(e) => {
                          const next = [...items];
                          next[i] = { ...item, institution: e.target.value };
                          onChange(next);
                        }}
                      />
                    )}
                  </FormField>
                  <FormField id={`edu-degree-${i}`} label="Degree">
                    {(props) => (
                      <Input
                        {...props}
                        value={item.degree ?? ""}
                        onChange={(e) => {
                          const next = [...items];
                          next[i] = { ...item, degree: e.target.value || null };
                          onChange(next);
                        }}
                      />
                    )}
                  </FormField>
                  <FormField id={`edu-field-${i}`} label="Field of study">
                    {(props) => (
                      <Input
                        {...props}
                        value={item.fieldOfStudy ?? ""}
                        onChange={(e) => {
                          const next = [...items];
                          next[i] = { ...item, fieldOfStudy: e.target.value || null };
                          onChange(next);
                        }}
                      />
                    )}
                  </FormField>
                  <div className="grid grid-cols-2 gap-3">
                    <FormField id={`edu-start-${i}`} label="Start">
                      {(props) => (
                        <Input
                          {...props}
                          type="date"
                          value={item.startDate ?? ""}
                          onChange={(e) => {
                            const next = [...items];
                            next[i] = { ...item, startDate: e.target.value || null };
                            onChange(next);
                          }}
                        />
                      )}
                    </FormField>
                    <FormField id={`edu-end-${i}`} label="End">
                      {(props) => (
                        <Input
                          {...props}
                          type="date"
                          value={item.endDate ?? ""}
                          onChange={(e) => {
                            const next = [...items];
                            next[i] = { ...item, endDate: e.target.value || null };
                            onChange(next);
                          }}
                        />
                      )}
                    </FormField>
                  </div>
                </div>
                <Button
                  type="button"
                  variant="ghost"
                  size="icon"
                  aria-label="Remove education entry"
                  onClick={() => onChange(items.filter((_, idx) => idx !== i))}
                >
                  <Trash2 />
                </Button>
              </div>
            </li>
          ))}
        </ul>
      )}
    </Card>
  );
}

function ExperienceSection({
  items,
  error,
  onChange,
}: {
  items: CandidateExperience[];
  error?: string;
  onChange: (items: CandidateExperience[]) => void;
}) {
  return (
    <Card className="space-y-4 p-6">
      <div className="flex items-center justify-between gap-3">
        <h2 className="text-h4 text-foreground">Experience</h2>
        <Button
          type="button"
          variant="outline"
          size="sm"
          onClick={() =>
            onChange([
              ...items,
              { id: null, companyName: "", title: "", startDate: null, endDate: null, description: null },
            ])
          }
        >
          <Plus /> Add experience
        </Button>
      </div>
      {error ? <p className="text-small text-error">{error}</p> : null}
      {items.length === 0 ? (
        <p className="text-small text-muted-foreground">No experience added yet.</p>
      ) : (
        <ul className="space-y-4">
          {items.map((item, i) => (
            <li key={item.id ?? i} className="space-y-3 rounded-md border border-border p-4">
              <div className="flex items-start justify-between gap-3">
                <div className="grid flex-1 gap-3 sm:grid-cols-2">
                  <FormField id={`exp-company-${i}`} label="Company">
                    {(props) => (
                      <Input
                        {...props}
                        value={item.companyName}
                        onChange={(e) => {
                          const next = [...items];
                          next[i] = { ...item, companyName: e.target.value };
                          onChange(next);
                        }}
                      />
                    )}
                  </FormField>
                  <FormField id={`exp-title-${i}`} label="Title">
                    {(props) => (
                      <Input
                        {...props}
                        value={item.title}
                        onChange={(e) => {
                          const next = [...items];
                          next[i] = { ...item, title: e.target.value };
                          onChange(next);
                        }}
                      />
                    )}
                  </FormField>
                  <div className="grid grid-cols-2 gap-3 sm:col-span-2">
                    <FormField id={`exp-start-${i}`} label="Start">
                      {(props) => (
                        <Input
                          {...props}
                          type="date"
                          value={item.startDate ?? ""}
                          onChange={(e) => {
                            const next = [...items];
                            next[i] = { ...item, startDate: e.target.value || null };
                            onChange(next);
                          }}
                        />
                      )}
                    </FormField>
                    <FormField id={`exp-end-${i}`} label="End" hint="Leave blank if current.">
                      {(props) => (
                        <Input
                          {...props}
                          type="date"
                          value={item.endDate ?? ""}
                          onChange={(e) => {
                            const next = [...items];
                            next[i] = { ...item, endDate: e.target.value || null };
                            onChange(next);
                          }}
                        />
                      )}
                    </FormField>
                  </div>
                  <FormField id={`exp-description-${i}`} label="Description" className="sm:col-span-2">
                    {(props) => (
                      <Textarea
                        {...props}
                        value={item.description ?? ""}
                        onChange={(e) => {
                          const next = [...items];
                          next[i] = { ...item, description: e.target.value || null };
                          onChange(next);
                        }}
                        rows={2}
                      />
                    )}
                  </FormField>
                </div>
                <Button
                  type="button"
                  variant="ghost"
                  size="icon"
                  aria-label="Remove experience entry"
                  onClick={() => onChange(items.filter((_, idx) => idx !== i))}
                >
                  <Trash2 />
                </Button>
              </div>
            </li>
          ))}
        </ul>
      )}
    </Card>
  );
}

function CertificationsSection({
  items,
  error,
  onChange,
}: {
  items: CandidateCertification[];
  error?: string;
  onChange: (items: CandidateCertification[]) => void;
}) {
  return (
    <Card className="space-y-4 p-6">
      <div className="flex items-center justify-between gap-3">
        <h2 className="text-h4 text-foreground">Certifications</h2>
        <Button
          type="button"
          variant="outline"
          size="sm"
          onClick={() =>
            onChange([
              ...items,
              { id: null, name: "", issuingOrganization: null, issueDate: null, credentialUrl: null },
            ])
          }
        >
          <Plus /> Add certification
        </Button>
      </div>
      {error ? <p className="text-small text-error">{error}</p> : null}
      {items.length === 0 ? (
        <p className="text-small text-muted-foreground">No certifications added yet.</p>
      ) : (
        <ul className="space-y-4">
          {items.map((item, i) => (
            <li key={item.id ?? i} className="space-y-3 rounded-md border border-border p-4">
              <div className="flex items-start justify-between gap-3">
                <div className="grid flex-1 gap-3 sm:grid-cols-2">
                  <FormField id={`cert-name-${i}`} label="Name">
                    {(props) => (
                      <Input
                        {...props}
                        value={item.name}
                        onChange={(e) => {
                          const next = [...items];
                          next[i] = { ...item, name: e.target.value };
                          onChange(next);
                        }}
                      />
                    )}
                  </FormField>
                  <FormField id={`cert-org-${i}`} label="Issuing organization">
                    {(props) => (
                      <Input
                        {...props}
                        value={item.issuingOrganization ?? ""}
                        onChange={(e) => {
                          const next = [...items];
                          next[i] = { ...item, issuingOrganization: e.target.value || null };
                          onChange(next);
                        }}
                      />
                    )}
                  </FormField>
                  <FormField id={`cert-date-${i}`} label="Issue date">
                    {(props) => (
                      <Input
                        {...props}
                        type="date"
                        value={item.issueDate ?? ""}
                        onChange={(e) => {
                          const next = [...items];
                          next[i] = { ...item, issueDate: e.target.value || null };
                          onChange(next);
                        }}
                      />
                    )}
                  </FormField>
                  <FormField id={`cert-url-${i}`} label="Credential URL">
                    {(props) => (
                      <Input
                        {...props}
                        type="url"
                        value={item.credentialUrl ?? ""}
                        onChange={(e) => {
                          const next = [...items];
                          next[i] = { ...item, credentialUrl: e.target.value || null };
                          onChange(next);
                        }}
                      />
                    )}
                  </FormField>
                </div>
                <Button
                  type="button"
                  variant="ghost"
                  size="icon"
                  aria-label="Remove certification entry"
                  onClick={() => onChange(items.filter((_, idx) => idx !== i))}
                >
                  <Trash2 />
                </Button>
              </div>
            </li>
          ))}
        </ul>
      )}
    </Card>
  );
}
