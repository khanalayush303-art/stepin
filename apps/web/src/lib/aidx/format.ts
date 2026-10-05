/**
 * Formatting for AIDX content. Dates are rendered in one fixed timezone so the server
 * output is deterministic regardless of where the server runs.
 */

const TIME_ZONE = "Australia/Sydney";

export function formatDate(value: string | null | undefined): string | null {
  if (!value) return null;
  // DateOnly values arrive as "yyyy-mm-dd" and must not shift by a timezone.
  const date = /^\d{4}-\d{2}-\d{2}$/.test(value) ? new Date(`${value}T00:00:00Z`) : new Date(value);
  const options: Intl.DateTimeFormatOptions = /^\d{4}-\d{2}-\d{2}$/.test(value)
    ? { day: "numeric", month: "long", year: "numeric", timeZone: "UTC" }
    : { day: "numeric", month: "long", year: "numeric", timeZone: TIME_ZONE };
  return new Intl.DateTimeFormat("en-AU", options).format(date);
}

export function formatDateTime(value: string): string {
  return new Intl.DateTimeFormat("en-AU", {
    day: "numeric",
    month: "long",
    year: "numeric",
    hour: "numeric",
    minute: "2-digit",
    timeZone: TIME_ZONE,
  }).format(new Date(value));
}

/**
 * Returns the URL only when it is http or https, otherwise null. The API already rejects other
 * schemes, so this is a second line of defence before a stored value becomes an href.
 */
export function safeHttpUrl(value: string | null | undefined): string | null {
  if (!value) return null;
  try {
    const url = new URL(value);
    return url.protocol === "http:" || url.protocol === "https:" ? value : null;
  } catch {
    return null;
  }
}

/** ISO value for a <time> element's dateTime attribute. */
export function isoDate(value: string): string {
  return new Date(value).toISOString();
}

const EMPLOYMENT: Record<string, string> = {
  FullTime: "Full-time",
  PartTime: "Part-time",
  Contract: "Contract",
  Internship: "Internship",
  Casual: "Casual",
};

const WORKPLACE: Record<string, string> = { OnSite: "On-site", Hybrid: "Hybrid", Remote: "Remote" };

const RESEARCHER: Record<string, string> = {
  Academic: "Academic",
  ResearchAssistant: "Research assistant",
  PhdStudent: "PhD student",
  ResearchStudent: "Research student",
  Alumni: "Alumni",
};

const PUBLICATION: Record<string, string> = {
  JournalArticle: "Journal article",
  ConferencePaper: "Conference paper",
  Report: "Report",
  BookChapter: "Book chapter",
  Dataset: "Dataset",
  TechnicalPublication: "Technical publication",
};

export const labels = {
  employment: (value: string) => EMPLOYMENT[value] ?? value,
  workplace: (value: string) => WORKPLACE[value] ?? value,
  researcher: (value: string) => RESEARCHER[value] ?? value,
  publication: (value: string) => PUBLICATION[value] ?? value,
};

export const PUBLICATION_TYPES = Object.keys(PUBLICATION);
export const RESEARCHER_CATEGORIES = Object.keys(RESEARCHER);
