import type { AdminPublicationDetail, PublicationInput } from "@/lib/aidx/publications-cms";
import type { PublicationFormValue, ProjectOption } from "./publication-form";
import { newAuthorKey } from "./author-editor";

export function emptyPublication(): PublicationFormValue {
  return {
    title: "",
    abstract: null,
    publicationType: "JournalArticle",
    venue: null,
    year: new Date().getFullYear(),
    doi: null,
    externalUrl: null,
    published: false,
    authors: [],
    researchAreaIds: [],
    projectIds: [],
  };
}

/** Loaded publication to form value. Authors keep their stored order. */
export function fromDetail(detail: AdminPublicationDetail): PublicationFormValue {
  return {
    title: detail.title,
    abstract: detail.abstract,
    publicationType: detail.publicationType,
    venue: detail.venue,
    year: detail.year,
    doi: detail.doi,
    externalUrl: detail.externalUrl,
    published: detail.published,
    authors: detail.authors.map((a) => ({
      key: newAuthorKey(),
      researcherId: a.researcherId,
      externalAuthorName: a.externalAuthorName,
      displayName: a.displayName,
      researcherPublished: a.researcherPublished,
    })),
    researchAreaIds: detail.researchAreaIds,
    projectIds: detail.projects.map((p) => p.id),
  };
}

/** Form value to the request body. Relationships are sent as complete, ordered sets. */
export function toInput(value: PublicationFormValue): PublicationInput {
  return {
    title: value.title,
    abstract: value.abstract,
    publicationType: value.publicationType,
    venue: value.venue,
    year: value.year,
    doi: value.doi,
    externalUrl: value.externalUrl,
    published: value.published,
    authors: value.authors.map((a) => ({ researcherId: a.researcherId, externalAuthorName: a.externalAuthorName })),
    researchAreaIds: value.researchAreaIds,
    projectIds: value.projectIds,
  };
}

/**
 * Project options: published projects, plus any project already linked to this publication. A linked
 * project that is no longer published stays in the list, labelled, so an admin can see and remove it.
 */
export function projectOptions(published: { id: string; title: string }[], linked: AdminPublicationDetail["projects"]): ProjectOption[] {
  const options: ProjectOption[] = published.map((p) => ({ id: p.id, title: p.title, statusNote: null }));
  for (const project of linked) {
    if (!options.some((o) => o.id === project.id)) {
      options.push({ id: project.id, title: project.title, statusNote: project.status === "Published" ? null : project.status.toLowerCase() });
    }
  }
  return options;
}
