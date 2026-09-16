import { redirect } from "next/navigation";

// Email verification is now part of Clerk's own <SignUp/> flow — this route
// only exists to send old bookmarks/links somewhere useful.
export default function VerifyEmailPage() {
  redirect("/sign-in");
}
