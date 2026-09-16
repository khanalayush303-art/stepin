import { SignIn } from "@clerk/nextjs";

export default function SignInPage() {
  return (
    <div className="space-y-5">
      <header className="space-y-1.5">
        <h1 className="text-h2 text-foreground">Welcome back</h1>
        <p className="text-body text-muted-foreground">Sign in to continue to your dashboard.</p>
      </header>

      <SignIn path="/sign-in" routing="path" signUpUrl="/register" />
    </div>
  );
}
