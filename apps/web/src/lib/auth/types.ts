/**
 * Shapes mirroring the real StepIn.Api auth contract (apps/api/src/StepIn.Api/Endpoints/AuthContracts.cs).
 * Unlike src/lib/types.ts, these describe a live API — keep them in sync with
 * the backend DTOs rather than the Phase 0 placeholder convention.
 */

export type UserRole = "Applicant" | "Recruiter" | "Admin";

export interface AuthUser {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  role: UserRole;
  emailConfirmed: boolean;
  accountStatus: string;
}

export interface PasswordPolicy {
  requiredLength: number;
  requireDigit: boolean;
  requireLowercase: boolean;
  requireUppercase: boolean;
  requireNonAlphanumeric: boolean;
}

export function dashboardPathForRole(role: UserRole): string {
  switch (role) {
    case "Recruiter":
      return "/recruiter";
    case "Admin":
      return "/admin";
    default:
      return "/dashboard";
  }
}
