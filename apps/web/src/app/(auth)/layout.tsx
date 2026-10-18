import { Check } from "lucide-react";
import { Logo } from "@/components/layout/logo";

const PROOF = [
  "128 verified employers",
  "96% of applications get a status update",
  "Built for students and graduates in Australia",
];

export default function AuthLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex min-h-screen flex-col lg:flex-row">
      {/*
        Brand panel — decorative reassurance, hidden from narrow screens. Kept on the
        app's light surface rather than a solid brand-colored fill: the approved logo
        artwork is an opaque PNG (no transparent version was supplied), so a colored
        panel behind it would show as a visible white box. A light surface lets the
        logo's own blue/cyan gradient read cleanly instead.
      */}
      <div className="flex flex-col justify-between gap-10 border-b border-border bg-surface p-8 lg:w-[46%] lg:max-w-[560px] lg:border-b-0 lg:border-r lg:p-16">
        <Logo variant="full" />

        <div className="hidden space-y-6 lg:block">
          <h2 className="text-h1 text-foreground">
            Every application,
            <br />
            in one place.
          </h2>
          <p className="text-body-lg text-muted-foreground">
            Track each stage from submitted through to outcome, with verified employers on
            the other side.
          </p>
          <ul className="space-y-3.5">
            {PROOF.map((item) => (
              <li key={item} className="flex items-center gap-2.5 text-body text-foreground-secondary">
                <Check className="size-[18px] shrink-0 text-primary" aria-hidden="true" />
                {item}
              </li>
            ))}
          </ul>
        </div>

        <p className="hidden text-caption text-muted-foreground lg:block">
          WCAG 2.1 AA &middot; Your data is never sold
        </p>
      </div>

      <main id="main" className="flex flex-1 items-center justify-center p-6 lg:p-16">
        <div className="w-full max-w-[400px]">{children}</div>
      </main>
    </div>
  );
}
