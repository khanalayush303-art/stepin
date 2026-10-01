/**
 * Mirrors the real StepIn.Api SavedJobResponse contract
 * (apps/api/src/StepIn.Api/Endpoints/SavedJobContracts.cs).
 *
 * Deliberately not a nested PublicJobSummary: that type assumes a
 * Published-only job and has no status field, but a saved job's underlying
 * job can later become Unpublished — jobStatus is how the UI detects that
 * without a second request.
 */
export interface SavedJob {
  id: string;
  savedAt: string;
  jobId: string;
  jobTitle: string;
  companyName: string;
  employmentType: string;
  workplaceType: string;
  location: string;
  compensation: string | null;
  jobStatus: string;
}
