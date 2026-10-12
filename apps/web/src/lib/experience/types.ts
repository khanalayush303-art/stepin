/** Mirrors apps/api/src/StepIn.Api/Endpoints/Experience{,Admin}Contracts.cs. */

export type ExperienceRecordType = "Employment" | "Internship" | "Project" | "Research" | "Volunteering" | "Certification";
export type ExperienceVisibility = "Private" | "Public";

/**
 * See apps/api/src/StepIn.Domain/Experience/ExperienceVerificationStatus.cs for the full
 * server-enforced transition table. A record is only ever editable while Draft, Rejected, or
 * Withdrawn (see ExperienceRecord.canEdit) — never while Submitted, UnderReview, or Verified.
 */
export type ExperienceVerificationStatus = "Draft" | "Submitted" | "UnderReview" | "Verified" | "Rejected" | "Withdrawn";

export interface ExperienceRecordInput {
  recordType: ExperienceRecordType;
  title: string;
  organization: string | null;
  description: string | null;
  startDate: string | null;
  endDate: string | null;
  skills: string[];
  evidenceUrl: string | null;
  visibility: ExperienceVisibility;
}

/** A candidate's own timeline entry — deliberately never includes the reviewer's identity. */
export interface ExperienceTimelineEntry {
  action: string;
  previousStatus: ExperienceVerificationStatus;
  newStatus: ExperienceVerificationStatus;
  note: string | null;
  createdAt: string;
}

export interface ExperienceRecord {
  id: string;
  recordType: ExperienceRecordType;
  title: string;
  organization: string | null;
  description: string | null;
  startDate: string | null;
  endDate: string | null;
  skills: string[];
  evidenceUrl: string | null;
  visibility: ExperienceVisibility;
  status: ExperienceVerificationStatus;
  canEdit: boolean;
  submittedAt: string | null;
  createdAt: string;
  updatedAt: string | null;
  timeline: ExperienceTimelineEntry[];
}

export interface ExperienceRecordSummary {
  id: string;
  recordType: ExperienceRecordType;
  title: string;
  organization: string | null;
  visibility: ExperienceVisibility;
  status: ExperienceVerificationStatus;
  startDate: string | null;
  endDate: string | null;
  createdAt: string;
  updatedAt: string | null;
}

// ---------------------------------------------------------------------- admin ---

export interface ExperienceQueueItem {
  id: string;
  recordType: ExperienceRecordType;
  title: string;
  organization: string | null;
  applicantName: string;
  status: ExperienceVerificationStatus;
  submittedAt: string | null;
  createdAt: string;
}

/** The admin-facing equivalent of ExperienceTimelineEntry — includes the reviewing actor's
 * identity, which a candidate's own timeline never shows. */
export interface ExperienceAuditEntry {
  action: string;
  previousStatus: ExperienceVerificationStatus;
  newStatus: ExperienceVerificationStatus;
  actorName: string;
  actorRole: string;
  note: string | null;
  createdAt: string;
}

export interface ExperienceAdminRecord {
  id: string;
  recordType: ExperienceRecordType;
  title: string;
  organization: string | null;
  description: string | null;
  startDate: string | null;
  endDate: string | null;
  skills: string[];
  evidenceUrl: string | null;
  visibility: ExperienceVisibility;
  status: ExperienceVerificationStatus;
  applicantName: string;
  applicantEmail: string;
  submittedAt: string | null;
  createdAt: string;
  updatedAt: string | null;
  auditHistory: ExperienceAuditEntry[];
}
