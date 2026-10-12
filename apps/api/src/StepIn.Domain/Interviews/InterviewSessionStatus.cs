namespace StepIn.Domain.Interviews;

/// <summary>
/// InProgress sessions allow answers to be saved and overwritten; a Completed session is
/// read-only (no retry workflow exists in this phase — a candidate starts a fresh session to try
/// again).
/// </summary>
public enum InterviewSessionStatus
{
    InProgress,
    Completed,
}
