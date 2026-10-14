export interface AidxWorkspaceSummary {
  myDrafts: number;
  mySubmitted: number;
  myUnderReview: number;
  myChangesRequested: number;
  myPublished: number;
  myArchived: number;
}

export interface AidxManageSummary {
  projectsAwaitingReview: number | null;
  newsAwaitingReview: number | null;
  eventsAwaitingReview: number | null;
  publicationsAwaitingReview: number | null;
}

export interface AidxWorkspaceItem {
  contentType: "Project" | "News" | "Event" | "Publication";
  id: string;
  title: string;
  status: string;
  revisionOfId: string | null;
  updatedAt: string | null;
}
