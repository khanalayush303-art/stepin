import { SignUp } from "@clerk/nextjs";

export default function RegisterPage() {
  return (
    <div className="space-y-5">
      <header className="space-y-1.5">
        <h1 className="text-h2 text-foreground">Create your account</h1>
        <p className="text-body text-muted-foreground">
          Free for students and graduates. No card required.
        </p>
      </header>

      <SignUp path="/register" routing="path" signInUrl="/sign-in" />
    </div>
  );
}
