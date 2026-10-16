import { safeHttpUrl } from "./format";

/**
 * A deliberately small formatting syntax (bold/italic/code/links/bullet lists, paragraphs split on
 * blank lines) rather than full Markdown or a WYSIWYG editor. Parses straight to a typed AST that
 * the renderer turns into React elements — there is no HTML string at any point, so there is
 * nothing to sanitize or inject: unsafe syntax (anything but http(s)/mailto links) is simply never
 * recognized as a link and falls back to plain text. The same AST/renderer is shared by the admin
 * and workspace draft preview and the public pages, so what an author sees while editing is
 * byte-for-byte what a visitor sees.
 */

export type RichInline =
  | { kind: "text"; value: string }
  | { kind: "bold"; children: RichInline[] }
  | { kind: "italic"; children: RichInline[] }
  | { kind: "code"; value: string }
  | { kind: "link"; href: string; children: RichInline[] };

export type RichBlock =
  | { kind: "paragraph"; children: RichInline[] }
  | { kind: "list"; items: RichInline[][] };

export function parseRichText(source: string): RichBlock[] {
  const blocks = source
    .split(/\n\s*\n/)
    .map((block) => block.trim())
    .filter(Boolean);

  return blocks.map((block) => {
    const lines = block.split("\n").map((line) => line.trim());
    const isList = lines.length > 0 && lines.every((line) => /^[-*]\s+/.test(line));
    if (isList) {
      return { kind: "list", items: lines.map((line) => parseInline(line.replace(/^[-*]\s+/, ""))) };
    }
    return { kind: "paragraph", children: parseInline(block.replace(/\n/g, " ")) };
  });
}

/** Link text/URL pairs, bold/italic spans and inline code — matched left to right, non-overlapping. */
const INLINE_PATTERN = /\[([^\]]+)\]\(([^)]+)\)|\*\*([^*]+)\*\*|\*([^*]+)\*|_([^_]+)_|`([^`]+)`/g;

function parseInline(text: string): RichInline[] {
  const nodes: RichInline[] = [];
  let lastIndex = 0;
  for (const match of text.matchAll(INLINE_PATTERN)) {
    const index = match.index ?? 0;
    if (index > lastIndex) {
      nodes.push({ kind: "text", value: text.slice(lastIndex, index) });
    }

    const [, linkText, linkHref, bold, italicStar, italicUnderscore, code] = match;
    if (linkText !== undefined && linkHref !== undefined) {
      const href = safeHttpUrl(linkHref) ?? (linkHref.startsWith("mailto:") ? linkHref : null);
      nodes.push(
        href
          ? { kind: "link", href, children: parseInline(linkText) }
          : { kind: "text", value: match[0] }
      );
    } else if (bold !== undefined) {
      nodes.push({ kind: "bold", children: parseInline(bold) });
    } else if (italicStar !== undefined) {
      nodes.push({ kind: "italic", children: parseInline(italicStar) });
    } else if (italicUnderscore !== undefined) {
      nodes.push({ kind: "italic", children: parseInline(italicUnderscore) });
    } else if (code !== undefined) {
      nodes.push({ kind: "code", value: code });
    }

    lastIndex = index + match[0].length;
  }
  if (lastIndex < text.length) {
    nodes.push({ kind: "text", value: text.slice(lastIndex) });
  }
  return nodes;
}
