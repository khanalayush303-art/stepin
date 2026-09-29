"use client";

import { Card } from "@/components/ui/card";
import { FormField } from "@/components/ui/form-field";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import type { JobInput } from "@/lib/jobs/types";

const EMPLOYMENT_TYPE_OPTIONS: { value: JobInput["employmentType"]; label: string }[] = [
  { value: "FullTime", label: "Full-time" },
  { value: "PartTime", label: "Part-time" },
  { value: "Contract", label: "Contract" },
  { value: "Internship", label: "Internship" },
  { value: "Casual", label: "Casual" },
];

const WORKPLACE_TYPE_OPTIONS: { value: JobInput["workplaceType"]; label: string }[] = [
  { value: "OnSite", label: "On-site" },
  { value: "Hybrid", label: "Hybrid" },
  { value: "Remote", label: "Remote" },
];

export function JobForm({
  value,
  onChange,
  fieldErrors,
  disabled,
}: {
  value: JobInput;
  onChange: (value: JobInput) => void;
  fieldErrors: Record<string, string[]>;
  disabled?: boolean;
}) {
  function update<K extends keyof JobInput>(key: K, next: JobInput[K]) {
    onChange({ ...value, [key]: next });
  }

  return (
    <div className="space-y-6">
      <Card className="space-y-4 p-6">
        <h2 className="text-h4 text-foreground">Role details</h2>
        <FormField id="title" label="Job title" error={fieldErrors.title?.[0]}>
          {(props) => (
            <Input
              {...props}
              value={value.title}
              onChange={(e) => update("title", e.target.value)}
              placeholder="Graduate Software Engineer"
              disabled={disabled}
            />
          )}
        </FormField>
        <FormField id="description" label="Description" error={fieldErrors.description?.[0]}>
          {(props) => (
            <Textarea
              {...props}
              value={value.description}
              onChange={(e) => update("description", e.target.value)}
              rows={8}
              disabled={disabled}
            />
          )}
        </FormField>
        <div className="grid gap-4 sm:grid-cols-2">
          <FormField id="employmentType" label="Employment type" error={fieldErrors.employmentType?.[0]}>
            {(props) => (
              <Select
                value={value.employmentType || undefined}
                onValueChange={(v) => update("employmentType", v as JobInput["employmentType"])}
                disabled={disabled}
              >
                <SelectTrigger id={props.id}>
                  <SelectValue placeholder="Select employment type" />
                </SelectTrigger>
                <SelectContent>
                  {EMPLOYMENT_TYPE_OPTIONS.map((option) => (
                    <SelectItem key={option.value} value={option.value}>
                      {option.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            )}
          </FormField>
          <FormField id="workplaceType" label="Workplace type" error={fieldErrors.workplaceType?.[0]}>
            {(props) => (
              <Select
                value={value.workplaceType || undefined}
                onValueChange={(v) => update("workplaceType", v as JobInput["workplaceType"])}
                disabled={disabled}
              >
                <SelectTrigger id={props.id}>
                  <SelectValue placeholder="Select workplace type" />
                </SelectTrigger>
                <SelectContent>
                  {WORKPLACE_TYPE_OPTIONS.map((option) => (
                    <SelectItem key={option.value} value={option.value}>
                      {option.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            )}
          </FormField>
        </div>
        <div className="grid gap-4 sm:grid-cols-2">
          <FormField id="location" label="Location" error={fieldErrors.location?.[0]}>
            {(props) => (
              <Input
                {...props}
                value={value.location}
                onChange={(e) => update("location", e.target.value)}
                placeholder="Sydney, NSW"
                disabled={disabled}
              />
            )}
          </FormField>
          <FormField id="compensation" label="Compensation" hint="Optional." error={fieldErrors.compensation?.[0]}>
            {(props) => (
              <Input
                {...props}
                value={value.compensation ?? ""}
                onChange={(e) => update("compensation", e.target.value || null)}
                placeholder="$70k–80k + super"
                disabled={disabled}
              />
            )}
          </FormField>
        </div>
        <FormField id="skills" label="Skills" hint="Separate with commas." error={fieldErrors.skills?.[0]}>
          {(props) => (
            <Input
              {...props}
              value={value.skills.join(", ")}
              onChange={(e) =>
                update(
                  "skills",
                  e.target.value.split(",").map((s) => s.trim()).filter((s) => s.length > 0)
                )
              }
              placeholder="C#, TypeScript, SQL"
              disabled={disabled}
            />
          )}
        </FormField>
      </Card>
    </div>
  );
}
