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

// -------------------------------------------------------- candidate/public ---

/**
 * The public shape of a job (PublicJobSummaryResponse/PublicJobResponse) —
 * deliberately has no companyId, no recruiterProfileId, and no createdAt/
 * updatedAt beyond publishedAt. Never mix these with the recruiter-facing
 * Job/JobSummary types above, which carry ownership-adjacent fields those
 * responses don't have reason to expose publicly.
 */
export interface PublicJobSummary {
  id: string;
  title: string;
  companyName: string;
  employmentType: EmploymentType;
  workplaceType: WorkplaceType;
  location: string;
  compensation: string | null;
  publishedAt: string;
}

export interface PublicJob {
  id: string;
  title: string;
  description: string;
  employmentType: EmploymentType;
  workplaceType: WorkplaceType;
  location: string;
  compensation: string | null;
  skills: string[];
  companyName: string;
  companyDescription: string | null;
  companyWebsite: string | null;
  companyLogoUrl: string | null;
  companyIndustry: string | null;
  companyLocation: string | null;
  publishedAt: string;
}

export interface JobFilters {
  search?: string;
  employmentType?: EmploymentType;
  workplaceType?: WorkplaceType;
  location?: string;
}
