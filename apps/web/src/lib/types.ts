/**
 * Domain shapes used by the Phase 0 UI.
 *
 * These describe what the interface renders, not the database schema. When the
 * API contract lands in Phase 1/2 these types should be regenerated from the
 * OpenAPI document the backend publishes, not hand-edited to match a mock.
 */

export type ApplicationStatus =
  | "Draft"
  | "Submitted"
  | "InReview"
  | "Interview"
  | "Offer"
  | "ClosingSoon"
  | "Unsuccessful"
  | "Withdrawn";

export type WorkType = "Full-time" | "Part-time" | "Casual" | "Internship" | "Contract";
export type WorkMode = "On-site" | "Hybrid" | "Remote";
export type ExperienceLevel = "No experience needed" | "Graduate" | "1-2 years";

export interface Company {
  id: string;
  name: string;
  slug: string;
  industry: string;
  size: string;
  location: string;
  verified: boolean;
  openRoles: number;
  tags: string[];
  summary: string;
}

export interface Job {
  id: string;
  slug: string;
  title: string;
  company: Pick<Company, "id" | "name" | "slug" | "verified">;
  location: string;
  workMode: WorkMode;
  workType: WorkType;
  experience: ExperienceLevel;
  compensation: string;
  summary: string;
  skills: string[];
  postedAt: string;
  closesAt: string;
  isInternship: boolean;
  /** Internship-only detail, shown on the Internships surface. */
  internship?: {
    durationWeeks: number;
    paid: boolean;
    cohortStart: string;
    convertsToGraduateRole: boolean;
  };
}

export type ApplicationStageState = "done" | "current" | "todo";

export interface ApplicationStage {
  name: string;
  state: ApplicationStageState;
  date?: string;
}

export interface Application {
  id: string;
  jobTitle: string;
  companyName: string;
  status: ApplicationStatus;
  updatedAt: string;
  stages: ApplicationStage[];
}

export interface Interview {
  id: string;
  jobTitle: string;
  companyName: string;
  scheduledFor: string;
  mode: string;
  durationMinutes: number;
}

export interface AppNotification {
  id: string;
  message: string;
  at: string;
  tone: "info" | "success" | "warning" | "error";
}

export interface Paged<T> {
  items: T[];
  page: number;
  pageSize: number;
  total: number;
}
