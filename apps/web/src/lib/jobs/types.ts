/**
 * Shapes mirroring the real StepIn.Api job contract
 * (apps/api/src/StepIn.Api/Endpoints/JobContracts.cs).
 */

export type JobStatus = "Draft" | "Published" | "Unpublished";
export type EmploymentType = "FullTime" | "PartTime" | "Contract" | "Internship" | "Casual";
export type WorkplaceType = "OnSite" | "Hybrid" | "Remote";

export interface JobSummary {
  id: string;
  title: string;
  companyName: string;
  status: JobStatus;
  createdAt: string;
  updatedAt: string | null;
  publishedAt: string | null;
}

export interface Job {
  id: string;
  title: string;
  description: string;
  employmentType: EmploymentType;
  workplaceType: WorkplaceType;
  location: string;
  compensation: string | null;
  skills: string[];
  status: JobStatus;
  companyId: string;
  companyName: string;
  createdAt: string;
  updatedAt: string | null;
  publishedAt: string | null;
}

/** No recruiterProfileId/companyId field, ever — the backend derives both server-side. */
export interface JobInput {
  title: string;
  description: string;
  employmentType: EmploymentType | "";
  workplaceType: WorkplaceType | "";
  location: string;
  compensation: string | null;
  skills: string[];
}
