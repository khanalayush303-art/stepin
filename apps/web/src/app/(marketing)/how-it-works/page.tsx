import type { Metadata } from "next";
import Link from "next/link";
import { BadgeCheck, ShieldCheck } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { PageHeader } from "@/components/layout/page-header";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { JOURNEY } from "@/lib/placeholder-data";

export const metadata: Metadata = {
  title: "How it works",
  description:
    "Discover, Match, Prepare, Apply, Track, Interview, Outcome — what each stage means for applicants and for recruiters.",
};

function Journey({ audience }: { audience: "applicant" | "recruiter" }) {
  return (
    <ol className="relative space-y-5 border-l border-border pl-6 sm:pl-8">
      {JOURNEY.map((step, i) => (
        <li key={step.key} className="relative">
          <span
            className="absolute -left-[34px] flex size-7 items-center justify-center rounded-full bg-primary text-caption font-semibold text-primary-foreground sm:-left-[42px]"
            aria-hidden="true"
          >
            {i + 1}
          </span>
          <Card className="p-5">
            <h3 className="text-h4 text-foreground">{step.title}</h3>
            <p className="mt-1.5 text-small text-muted-foreground">{step[audience]}</p>
          </Card>
        </li>
      ))}
    </ol>
  );
}

export default function HowItWorksPage() {
  return (
    <>
      <PageHeader
        crumbs={[{ label: "Home", href: "/" }, { label: "How it works" }]}
        title="How StepIn works"
        description="The same seven stages for everyone. An applicant always sees where they are; a recruiter always sees where each candidate is."
      />

      <section className="container-page py-10 lg:py-14">
        <Tabs defaultValue="applicant">
          <TabsList aria-label="Choose an audience">
            <TabsTrigger value="applicant">If you are applying</TabsTrigger>
            <TabsTrigger value="recruiter">If you are hiring</TabsTrigger>
          </TabsList>
          <TabsContent value="applicant">
            <Journey audience="applicant" />
          </TabsContent>
          <TabsContent value="recruiter">
            <Journey audience="recruiter" />
          </TabsContent>
        </Tabs>
      </section>

      <section className="border-t border-border bg-surface py-12 lg:py-16">
        <div className="container-page grid gap-6 lg:grid-cols-2">
          <Card className="flex gap-4 p-6">
            <span
              className="flex size-11 shrink-0 items-center justify-center rounded-md bg-accent-subtle text-accent-subtle-fg"
              aria-hidden="true"
            >
              <ShieldCheck className="size-5" />
            </span>
            <div className="space-y-1.5">
              <h2 className="text-h4 text-foreground">What verification actually checks</h2>
              <p className="text-small text-muted-foreground">
                A registered ABN matching the trading name, a hiring contact on the
                company&apos;s own email domain, and a review of the first role they post. An
                employer who fails any check cannot publish.
              </p>
            </div>
          </Card>

          <Card className="flex gap-4 p-6">
            <span
              className="flex size-11 shrink-0 items-center justify-center rounded-md bg-primary-subtle text-primary"
              aria-hidden="true"
            >
              <BadgeCheck className="size-5" />
            </span>
            <div className="space-y-1.5">
              <h2 className="text-h4 text-foreground">What we ask of employers</h2>
              <p className="text-small text-muted-foreground">
                Move every application to a real outcome. Roles that sit without a status
                update lose their featured placement, and applicants can see the
                employer&apos;s median response time before they apply.
              </p>
            </div>
          </Card>
        </div>

        <div className="container-page mt-10 flex flex-wrap justify-center gap-3">
          <Button size="lg" asChild>
            <Link href="/register">Create your account</Link>
          </Button>
          <Button size="lg" variant="outline" asChild>
            <Link href="/jobs">Browse roles first</Link>
          </Button>
        </div>
      </section>
    </>
  );
}
