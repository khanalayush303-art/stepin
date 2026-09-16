import type { Metadata, Viewport } from "next";
import { ClerkProvider } from "@clerk/nextjs";
import "@fontsource-variable/inter";
import "./globals.css";
import { TooltipProvider } from "@/components/ui/tooltip";
import { clerkAppearance } from "@/lib/auth/clerk-appearance";

export const metadata: Metadata = {
  title: {
    default: "StepIn — Find opportunities. Apply with confidence.",
    template: "%s · StepIn",
  },
  description:
    "Graduate roles and internships from verified employers, with every stage of your application visible in one place.",
  applicationName: "StepIn",
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
        <ClerkProvider appearance={clerkAppearance}>
          <TooltipProvider delayDuration={200}>{children}</TooltipProvider>
        </ClerkProvider>
      </body>
    </html>
  );
}
