/**
 * Shapes mirroring the real StepIn.Api profile contract
 * (apps/api/src/StepIn.Api/Endpoints/ProfileContracts.cs). Dates are ISO
 * `yyyy-MM-dd` strings (System.Text.Json's default for .NET's `DateOnly`).
 */

export interface CandidateEducation {
  id: string | null;
  institution: string;
  degree: string | null;
  fieldOfStudy: string | null;
  startDate: string | null;
  endDate: string | null;
  description: string | null;
}

export interface CandidateExperience {
  id: string | null;
  companyName: string;
  title: string;
  startDate: string | null;
  endDate: string | null;
  description: string | null;
}

export interface CandidateCertification {
  id: string | null;
  name: string;
  issuingOrganization: string | null;
  issueDate: string | null;
  credentialUrl: string | null;
}

export interface CandidateProfile {
  userId: string;
  email: string;
  firstName: string;
  lastName: string;
  phoneNumber: string | null;
  location: string | null;
  headline: string | null;
  bio: string | null;
  photoUrl: string | null;
  linkedInUrl: string | null;
  portfolioUrl: string | null;
  gitHubUrl: string | null;
  skills: string[];
  education: CandidateEducation[];
  experience: CandidateExperience[];
  certifications: CandidateCertification[];
  profileCompletionPercent: number;
}

export interface UpdateCandidateProfileInput {
  phoneNumber: string | null;
  location: string | null;
  headline: string | null;
  bio: string | null;
  photoUrl: string | null;
  linkedInUrl: string | null;
  portfolioUrl: string | null;
  gitHubUrl: string | null;
  skills: string[];
  education: CandidateEducation[];
  experience: CandidateExperience[];
  certifications: CandidateCertification[];
}

export interface Company {
  id: string | null;
  name: string;
  description: string | null;
  website: string | null;
  logoUrl: string | null;
  industry: string | null;
  location: string | null;
}

export interface RecruiterProfile {
  userId: string;
  email: string;
  firstName: string;
  lastName: string;
  jobTitle: string | null;
  phoneNumber: string | null;
  photoUrl: string | null;
  company: Company | null;
}

export interface UpdateRecruiterProfileInput {
  jobTitle: string | null;
  phoneNumber: string | null;
  photoUrl: string | null;
  company: Company | null;
}
