import type { Metadata, Viewport } from "next";
import { ClerkProvider } from "@clerk/nextjs";
import "@fontsource-variable/inter";
import "./globals.css";
import { TooltipProvider } from "@/components/ui/tooltip";
import { clerkAppearance } from "@/lib/auth/clerk-appearance";

export const metadata: Metadata = {
  title: {
    default: "StepIn: The AIDX Experience — Connecting Careers, Research & Innovation",
    template: "%s · StepIn",
  },
  description:
    "Connecting careers, research and innovation: graduate roles and internships from verified employers, plus the AIDX Lab's research, publications and events — all in one place.",
  applicationName: "StepIn: The AIDX Experience",
};

export const viewport: Viewport = {
  width: "device-width",
  initialScale: 1,
  themeColor: [
    { media: "(prefers-color-scheme: light)", color: "#f8fafc" },
    { media: "(prefers-color-scheme: dark)", color: "#020617" },
  ],
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en-AU" suppressHydrationWarning>
      <body style={{ ["--font-inter" as string]: "'Inter Variable'" }}>
        {/*
          afterSignOutUrl is the single source of truth Clerk itself redirects to once a
          session is actually cleared (it drives Clerk's own internal setActive navigation,
          which also forces a router.refresh() to invalidate the Next.js Router Cache — see
          DashboardShell's sign-out handler for why that matters). Any future <UserButton />
          or <SignOutButton /> inherits this automatically; nothing has to repeat it.
        */}
        <ClerkProvider appearance={clerkAppearance} afterSignOutUrl="/sign-in">
          <TooltipProvider delayDuration={200}>{children}</TooltipProvider>
        </ClerkProvider>
      </body>
    </html>
  );
}
