/**
 * Shapes mirroring the real StepIn.Api application contract
 * (apps/api/src/StepIn.Api/Endpoints/ApplicationContracts.cs).
 */

export type ApplicationStatus = "Submitted" | "Reviewed" | "Shortlisted" | "Rejected";

/** The same four values as ApplicationStatus, for iterating a status picker. */
export const APPLICATION_STATUSES: ApplicationStatus[] = ["Submitted", "Reviewed", "Shortlisted", "Rejected"];

export interface ApplicationSummary {
  id: string;
  jobId: string;
  jobTitle: string;
  companyName: string;
  status: ApplicationStatus;
  resumeFileName: string;
  createdAt: string;
}

export interface Application {
  id: string;
  jobId: string;
  jobTitle: string;
  companyName: string;
  status: ApplicationStatus;
  coverLetter: string | null;
  resumeFileName: string;
  createdAt: string;
  statusUpdatedAt: string;
}

export interface ApplicationEligibility {
  hasApplied: boolean;
  applicationId: string | null;
}

// ------------------------------------------------------------- recruiter ---
// Mirrors RecruiterApplicationContracts.cs. Deliberately a separate shape
// from Application/ApplicationSummary above (applicant name/email instead of
// company name) — never mix the two, since they come from different
// ownership-scoped endpoints.

export interface RecruiterApplicationSummary {
  id: string;
  jobId: string;
  jobTitle: string;
  applicantName: string;
  applicantEmail: string;
  status: ApplicationStatus;
  resumeFileName: string;
  createdAt: string;
}

export interface RecruiterApplication {
  id: string;
  jobId: string;
  jobTitle: string;
  applicantName: string;
  applicantEmail: string;
  status: ApplicationStatus;
  coverLetter: string | null;
  resumeFileName: string;
  createdAt: string;
  statusUpdatedAt: string;
}

export type ApplicationSort = "newest" | "oldest";

/** Mirrors the shape of JobFilters in lib/jobs/types.ts — same "omit to mean unfiltered" convention. */
export interface RecruiterApplicationFilters {
  status?: ApplicationStatus;
  search?: string;
  sort?: ApplicationSort;
}
