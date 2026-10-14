import Link from "next/link";
import { ArrowRight, BookUser, Compass, FolderGit2, Mic } from "lucide-react";
import { Card } from "@/components/ui/card";
import { CANDIDATE_NAV } from "@/components/dashboard/candidate-nav";
import { DashboardShell } from "@/components/dashboard/dashboard-shell";

const NAV = CANDIDATE_NAV;

// No single existing page was ever "Prepare" under a different name, and no preparation
// backend exists to build a dedicated one against — so this is a genuine hub, not a redirect:
// it links only to tools that already exist and already work, rather than inventing a new
// feature or faking content of its own.
const TOOLS = [
  {
    href: "/dashboard/career-gps",
    icon: Compass,
    title: "Career GPS",
    description: "Set a target role, break it into a roadmap, and see how your skills compare against a real job.",
  },
  {
    href: "/dashboard/interview-practice",
    icon: Mic,
    title: "Interview practice",
    description: "Practice with curated questions and get rubric-based feedback — simple heuristics, not AI-generated.",
  },
  {
    href: "/dashboard/portfolio",
    icon: FolderGit2,
    title: "Portfolio",
    description: "Showcase real projects and skills. Private by default — share only what you choose with recruiters you apply to.",
  },
  {
    href: "/dashboard/experience-passport",
    icon: BookUser,
    title: "Experience Passport",
    description: "Record your experience and optionally submit it for verification by a StepIn administrator.",
  },
] as const;

export default function PreparePage() {
  return (
    <DashboardShell nav={NAV} navLabel="Applicant dashboard">
      <div className="space-y-6">
        <div className="space-y-1">
          <h1 className="text-h2 text-foreground">Prepare</h1>
          <p className="text-body text-muted-foreground">
            Everything that helps you get ready for your next application or interview, in one place.
          </p>
        </div>

        <ul className="grid gap-4 sm:grid-cols-2">
          {TOOLS.map((tool) => {
            const Icon = tool.icon;
            return (
              <li key={tool.href}>
                <Card interactive className="relative flex h-full flex-col gap-3 p-6">
                  <span className="flex size-10 items-center justify-center rounded-full bg-primary-subtle text-primary" aria-hidden="true">
                    <Icon className="size-5" />
                  </span>
                  <h2 className="text-h4 text-foreground">
                    <Link href={tool.href} className="rounded-sm after:absolute after:inset-0 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring">
                      {tool.title}
                    </Link>
                  </h2>
                  <p className="flex-1 text-small text-foreground-secondary">{tool.description}</p>
                  <span className="inline-flex items-center gap-1 text-small font-medium text-primary">
                    Open
                    <ArrowRight className="size-4" />
                  </span>
                </Card>
              </li>
            );
          })}
        </ul>
      </div>
    </DashboardShell>
  );
}
