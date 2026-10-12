/** Mirrors apps/api/src/StepIn.Api/Endpoints/InterviewContracts.cs. */

export type InterviewCategory = "General" | "Behavioral" | "Technical" | "Situational";
export type InterviewSessionStatus = "InProgress" | "Completed";

export interface InterviewCategoryOption {
  category: InterviewCategory;
  availableQuestionCount: number;
}

export interface InterviewTemplates {
  categories: InterviewCategoryOption[];
  minQuestionCount: number;
  maxQuestionCount: number;
}

export interface InterviewFeedbackCriterion {
  name: string;
  met: boolean;
  note: string;
}

/**
 * Rubric-based practice feedback — simple keyword heuristics, never an AI call and never a
 * verified hiring assessment. disclaimer always says so; render it, don't hide it.
 */
export interface InterviewFeedback {
  answered: boolean;
  criteria: InterviewFeedbackCriterion[];
  criteriaMetCount: number;
  criteriaTotalCount: number;
  band: string;
  improvements: string[];
  suggestedStructure: string;
  disclaimer: string;
}

export interface InterviewQuestion {
  id: string;
  orderIndex: number;
  category: InterviewCategory;
  questionText: string;
  answerText: string | null;
  answeredAt: string | null;
  feedback: InterviewFeedback;
}

export interface InterviewSession {
  id: string;
  category: InterviewCategory;
  targetRole: string | null;
  status: InterviewSessionStatus;
  questionCount: number;
  answeredCount: number;
  createdAt: string;
  completedAt: string | null;
  questions: InterviewQuestion[];
}

export interface InterviewSessionSummary {
  id: string;
  category: InterviewCategory;
  targetRole: string | null;
  status: InterviewSessionStatus;
  questionCount: number;
  answeredCount: number;
  createdAt: string;
  completedAt: string | null;
}

export interface CreateInterviewSessionInput {
  category: InterviewCategory;
  targetRole: string | null;
  questionCount: number;
}
