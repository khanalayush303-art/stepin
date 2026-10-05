import { toIsoFromLocal, toLocalInput, type AdminEventDetail, type EventInput } from "@/lib/aidx/events-cms";
import type { EventFormValue } from "./event-form";

export function emptyEvent(): EventFormValue {
  return {
    title: "",
    slug: null,
    description: "",
    startsLocal: "",
    endsLocal: "",
    location: null,
    registrationUrl: null,
    speakerName: null,
  };
}

/** Loaded event to form value. Stored instants are shown in the browser's local time. */
export function fromDetail(detail: AdminEventDetail): EventFormValue {
  return {
    title: detail.title,
    slug: detail.slug,
    description: detail.description,
    startsLocal: toLocalInput(detail.startsAt),
    endsLocal: toLocalInput(detail.endsAt),
    location: detail.location,
    registrationUrl: detail.registrationUrl,
    speakerName: detail.speakerName,
  };
}

/**
 * Form value to request body. Returns the field errors to show when the start is missing or unreadable,
 * so the form never sends an event without a start time.
 */
export function toInput(value: EventFormValue): { input: EventInput; errors: Record<string, string[]> } {
  const errors: Record<string, string[]> = {};
  const startsAt = toIsoFromLocal(value.startsLocal);
  if (!startsAt) {
    errors.startsAt = ["Enter a start date and time."];
  }

  const endsAt = toIsoFromLocal(value.endsLocal);
  if (value.endsLocal && !endsAt) {
    errors.endsAt = ["Enter a valid end date and time."];
  }

  return {
    input: {
      title: value.title,
      slug: value.slug,
      description: value.description,
      startsAt: startsAt ?? "",
      endsAt,
      location: value.location,
      registrationUrl: value.registrationUrl,
      speakerName: value.speakerName,
    },
    errors,
  };
}
