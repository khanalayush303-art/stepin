/**
 * Public AIDX DTOs, mirroring apps/api/src/StepIn.Api/Endpoints/AidxContracts.cs.
 * Storage keys (image, PDF) are deliberately absent: the API never returns them.
 */

export interface PageResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface ResearchArea {
  id: string;
  name: string;
  slug: string;
  description: string | null;
}

export interface ProjectSummary {
  id: string;
  title: string;
  slug: string;
  shortDescription: string;
  featured: boolean;
  startDate: string | null;
  endDate: string | null;
  publishedAt: string | null;
  researchAreas: string[];
}

export interface ProjectResearcher {
  id: string;
  slug: string;
  displayName: string;
  role: string | null;
}

export interface ProjectDetail {
  id: string;
  title: string;
  slug: string;
  shortDescription: string;
  description: string;
  featured: boolean;
  startDate: string | null;
  endDate: string | null;
  externalUrl: string | null;
  publishedAt: string | null;
  researchAreas: string[];
  technologies: string[];
  researchers: ProjectResearcher[];
}

export type ResearcherCategory = "Academic" | "ResearchAssistant" | "PhdStudent" | "ResearchStudent" | "Alumni";

export interface Person {
  id: string;
  slug: string;
  displayName: string;
  category: ResearcherCategory;
  position: string | null;
  biography: string | null;
  orcidUrl: string | null;
  googleScholarUrl: string | null;
  linkedInUrl: string | null;
  websiteUrl: string | null;
}

export type PublicationType =
  | "JournalArticle"
  | "ConferencePaper"
  | "Report"
  | "BookChapter"
  | "Dataset"
  | "TechnicalPublication";

export interface Publication {
  id: string;
  title: string;
  abstract: string | null;
  publicationType: PublicationType;
  venue: string | null;
  year: number;
  doi: string | null;
  externalUrl: string | null;
  authors: string[];
}

export interface NewsSummary {
  id: string;
  slug: string;
  title: string;
  summary: string;
  publishedAt: string | null;
}

export interface NewsDetail {
  id: string;
  slug: string;
  title: string;
  summary: string;
  body: string;
  publishedAt: string | null;
}

export interface AidxEvent {
  id: string;
  slug: string;
  title: string;
  description: string;
  startsAt: string;
  endsAt: string | null;
  location: string | null;
  registrationUrl: string | null;
  speakerName: string | null;
}

/** A research opportunity. Links to the StepIn job detail, never to an AIDX-specific page. */
export interface Opportunity {
  id: string;
  title: string;
  companyName: string;
  location: string;
  employmentType: string;
  workplaceType: string;
  compensation: string | null;
  skills: string[];
  publishedAt: string | null;
  projectSlug: string | null;
  projectTitle: string | null;
}
