/**
 * Shapes mirroring the real StepIn.Api application contract
 * (apps/api/src/StepIn.Api/Endpoints/ApplicationContracts.cs).
 */

export type ApplicationStatus = "Submitted";

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
}

export interface ApplicationEligibility {
  hasApplied: boolean;
  applicationId: string | null;
}
