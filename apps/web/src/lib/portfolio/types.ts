/** Mirrors apps/api/src/StepIn.Api/Endpoints/PortfolioContracts.cs. */

export type PortfolioVisibility = "Private" | "Public";

/**
 * Visibility defaults to Private on the backend — a project is only ever visible to someone else
 * once the candidate explicitly sets it Public, and even then only through the existing recruiter
 * application-review flow, never a public browsing page.
 */
export interface PortfolioProject {
  id: string;
  title: string;
  description: string;
  skills: string[];
  startDate: string | null;
  endDate: string | null;
  repositoryUrl: string | null;
  demoUrl: string | null;
  visibility: PortfolioVisibility;
  researchProjectId: string | null;
  researchProjectTitle: string | null;
  createdAt: string;
  updatedAt: string | null;
}

export interface PortfolioProjectInput {
  title: string;
  description: string;
  skills: string[];
  startDate: string | null;
  endDate: string | null;
  repositoryUrl: string | null;
  demoUrl: string | null;
  visibility: PortfolioVisibility;
  researchProjectId: string | null;
}

/** What a recruiter sees, through the applicant's application detail only — never a direct browse. */
export interface PublicPortfolioProject {
  id: string;
  title: string;
  description: string;
  skills: string[];
  startDate: string | null;
  endDate: string | null;
  repositoryUrl: string | null;
  demoUrl: string | null;
  researchProjectTitle: string | null;
}
