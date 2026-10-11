/** Mirrors apps/api/src/StepIn.Api/Endpoints/CareerGoalContracts.cs. */

export type CareerGoalStatus = "Active" | "Completed" | "Archived";
export type RoadmapItemStatus = "NotStarted" | "InProgress" | "Completed";
export type RoadmapItemPriority = "Low" | "Medium" | "High";

/**
 * progressPercent is always computed server-side from roadmapItemCount/completedRoadmapItemCount
 * — never trust a locally-derived percentage instead. 0 with roadmapItemCount === 0 means "no
 * items yet", not "0% done"; the UI should show an empty state in that case, not a progress bar.
 */
export interface CareerGoal {
  id: string;
  targetRole: string;
  description: string | null;
  targetDate: string | null;
  status: CareerGoalStatus;
  isPrimary: boolean;
  targetJobId: string | null;
  targetJobTitle: string | null;
  targetJobCompanyName: string | null;
  roadmapItemCount: number;
  completedRoadmapItemCount: number;
  progressPercent: number;
  createdAt: string;
  updatedAt: string | null;
}

export interface CareerGoalInput {
  targetRole: string;
  description: string | null;
  targetDate: string | null;
  targetJobId: string | null;
}

/** isPrimary is only meaningful on update — a goal is never created already primary. */
export interface UpdateCareerGoalInput extends CareerGoalInput {
  isPrimary: boolean;
}

export interface RoadmapItem {
  id: string;
  careerGoalId: string;
  title: string;
  description: string | null;
  targetDate: string | null;
  priority: RoadmapItemPriority;
  status: RoadmapItemStatus;
  completedAt: string | null;
  createdAt: string;
  updatedAt: string | null;
}

export interface RoadmapItemInput {
  title: string;
  description: string | null;
  targetDate: string | null;
  priority: RoadmapItemPriority | null;
}

/**
 * Reuses the exact Transparent Job Match scoring rule (JobMatchScorer) against the goal's own
 * target job — matchQuality "Unscored" means the job recorded no skills to compare against, and
 * skillCoverage is null exactly then. Never an invented score.
 */
export interface CareerGoalSkillGap {
  jobId: string;
  jobTitle: string;
  companyName: string;
  matchedSkills: string[];
  skillsNotYetDemonstrated: string[];
  skillCoverage: number | null;
  matchQuality: "Unscored" | "ExploreMatch" | "PartialMatch" | "StrongMatch";
}
