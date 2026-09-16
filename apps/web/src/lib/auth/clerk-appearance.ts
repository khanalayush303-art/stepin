/** Maps Clerk's prebuilt components onto this app's own design tokens (see globals.css). */
export const clerkAppearance = {
  variables: {
    colorPrimary: "var(--color-primary)",
    colorBackground: "var(--color-surface)",
    colorText: "var(--color-foreground)",
    colorTextSecondary: "var(--color-muted-foreground)",
    colorDanger: "var(--color-error)",
    colorInputBackground: "var(--color-background)",
    colorInputText: "var(--color-foreground)",
    borderRadius: "var(--radius-md)",
    fontFamily: "var(--font-sans)",
  },
  elements: {
    rootBox: "w-full",
    card: "w-full shadow-none p-0 gap-4",
    // A Tailwind `hidden` class loses the cascade to Clerk's own later-injected
    // stylesheet — inline styles always win, so this is the one that actually
    // suppresses Clerk's <h1> and keeps this page's own heading the only one.
    header: { display: "none" },
    socialButtonsBlockButton: "border-border",
    formButtonPrimary: "h-control-md text-small normal-case shadow-none",
    formFieldInput: "h-control-md",
  },
};
