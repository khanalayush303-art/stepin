import type { PublicationDetail } from "./types";

/** Pure formatting only — no network, no state. Shared between the citation display and (if ever
 * needed elsewhere) any export feature, so there is one definition of what a citation looks like. */
export function formatApaCitation(publication: PublicationDetail): string {
  const authors = publication.authors.length ? `${publication.authors.join(", ")}.` : "";
  const year = `(${publication.year}).`;
  const title = `${publication.title}.`;
  const venue = publication.venue ? `${publication.venue}.` : "";
  const doi = publication.doi ? `https://doi.org/${publication.doi}` : "";

  return [authors, year, title, venue, doi].filter(Boolean).join(" ");
}

function bibtexKey(publication: PublicationDetail): string {
  const firstAuthor = publication.authors[0]?.split(" ").pop()?.replace(/[^A-Za-z]/g, "") ?? "aidx";
  return `${firstAuthor}${publication.year}`;
}

function bibtexEntryType(publicationType: string): string {
  switch (publicationType) {
    case "ConferencePaper":
      return "inproceedings";
    case "JournalArticle":
      return "article";
    case "Preprint":
      return "misc";
    case "Dataset":
      return "misc";
    default:
      return "misc";
  }
}

function escapeBibtex(value: string): string {
  return value.replace(/[{}]/g, "");
}

export function formatBibtex(publication: PublicationDetail): string {
  const entryType = bibtexEntryType(publication.publicationType);
  const key = bibtexKey(publication);
  const fields: string[] = [
    `  title = {${escapeBibtex(publication.title)}}`,
    publication.authors.length ? `  author = {${escapeBibtex(publication.authors.join(" and "))}}` : null,
    `  year = {${publication.year}}`,
    publication.venue ? `  ${entryType === "article" ? "journal" : "booktitle"} = {${escapeBibtex(publication.venue)}}` : null,
    publication.doi ? `  doi = {${publication.doi}}` : null,
  ].filter((line): line is string => line !== null);

  return `@${entryType}{${key},\n${fields.join(",\n")}\n}`;
}
