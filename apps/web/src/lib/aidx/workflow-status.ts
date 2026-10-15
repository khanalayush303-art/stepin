/** The full editorial-workflow status set, shared by every AIDX content type that goes through
 * it (Project, News, Event, Publication). Replaces each type's own narrower Draft/Published/
 * Archived-only alias, which no longer matched what the backend can actually return once a row
 * is mid-review. */
export type AidxWorkflowStatus = "Draft" | "Submitted" | "UnderReview" | "ChangesRequested" | "Published" | "Archived";
