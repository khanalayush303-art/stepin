import { redirect } from "next/navigation";

// Password reset is now part of Clerk's own <SignIn/> flow — this route only
// exists to send old bookmarks/links somewhere useful.
export default function ResetPasswordPage() {
  redirect("/sign-in");
}
