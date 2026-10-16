import { parseRichText, type RichInline } from "@/lib/aidx/rich-text";

function Inline({ nodes }: { nodes: RichInline[] }) {
  return (
    <>
      {nodes.map((node, index) => {
        switch (node.kind) {
          case "text":
            return node.value ? <span key={index}>{node.value}</span> : null;
          case "bold":
            return (
              <strong key={index}>
                <Inline nodes={node.children} />
              </strong>
            );
          case "italic":
            return (
              <em key={index}>
                <Inline nodes={node.children} />
              </em>
            );
          case "code":
            return (
              <code key={index} className="rounded bg-muted px-1 py-0.5 text-caption">
                {node.value}
              </code>
            );
          case "link":
            return (
              <a
                key={index}
                href={node.href}
                target={node.href.startsWith("mailto:") ? undefined : "_blank"}
                rel={node.href.startsWith("mailto:") ? undefined : "noreferrer"}
                className="font-medium text-primary underline-offset-4 hover:underline"
              >
                <Inline nodes={node.children} />
              </a>
            );
        }
      })}
    </>
  );
}

/**
 * Renders the small safe-formatting syntax defined in lib/aidx/rich-text.ts. Shared verbatim
 * between the admin/workspace draft preview and the public pages, so there is one renderer to
 * keep correct rather than several copies drifting apart.
 */
export function AidxRichText({
  text,
  className,
  proseClassName = "text-body leading-relaxed text-foreground-secondary",
}: {
  text: string;
  className?: string;
  /** Typography classes applied to each paragraph/list — override to match a page's own body size. */
  proseClassName?: string;
}) {
  if (!text.trim()) return null;
  const blocks = parseRichText(text);
  return (
    <div className={className}>
      {blocks.map((block, index) =>
        block.kind === "list" ? (
          <ul key={index} className={`list-disc space-y-1 pl-5 ${proseClassName}`}>
            {block.items.map((item, itemIndex) => (
              <li key={itemIndex}>
                <Inline nodes={item} />
              </li>
            ))}
          </ul>
        ) : (
          <p key={index} className={proseClassName}>
            <Inline nodes={block.children} />
          </p>
        )
      )}
    </div>
  );
}
