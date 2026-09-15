import { Breadcrumb, type Crumb } from "@/components/ui/breadcrumb";

export function PageHeader({
  crumbs,
  title,
  description,
  children,
}: {
  crumbs: Crumb[];
  title: string;
  description?: string;
  children?: React.ReactNode;
}) {
  return (
    <section className="border-b border-border bg-surface">
      <div className="container-page flex flex-col gap-5 py-10">
        <Breadcrumb items={crumbs} />
        <div className="space-y-1.5">
          <h1 className="text-h1 text-foreground">{title}</h1>
          {description ? (
            <p className="max-w-2xl text-body text-muted-foreground">{description}</p>
          ) : null}
        </div>
        {children}
      </div>
    </section>
  );
}
