/**
 * PHASE 0 DESIGN FIXTURES — NOT BUSINESS DATA.
 *
 * These records exist so the interface can be reviewed against the Figma file
 * before any backend exists. They are deliberately isolated in this one module
 * and referenced only by page components.
 *
 * Phase 2 replaces every import of this file with a call to the API. Nothing
 * here should be copied into the database, a seeder, or a migration.
 */

import type { Application, AppNotification, Company, Interview, Job } from "./types";

export const COMPANIES: Company[] = [
  {
    id: "c1", name: "Atlassian", slug: "atlassian", industry: "Software",
    size: "2,000+ employees", location: "Sydney, NSW", verified: true, openRoles: 14,
    tags: ["Technology", "Graduate program"],
    summary: "Collaboration software used by teams in over 190 countries.",
  },
  {
    id: "c2", name: "Canva", slug: "canva", industry: "Software",
    size: "3,000+ employees", location: "Sydney, NSW", verified: true, openRoles: 9,
    tags: ["Technology", "Internships"],
    summary: "Visual communication platform with a long-running intern cohort.",
  },
  {
    id: "c3", name: "NSW Health", slug: "nsw-health", industry: "Healthcare",
    size: "10,000+ employees", location: "Statewide, NSW", verified: true, openRoles: 26,
    tags: ["Health", "Graduate program"],
    summary: "Public health services across New South Wales.",
  },
  {
    id: "c4", name: "Commonwealth Bank", slug: "commonwealth-bank", industry: "Financial services",
    size: "10,000+ employees", location: "Sydney, NSW", verified: true, openRoles: 18,
    tags: ["Finance", "Graduate program"],
    summary: "Retail and business banking with a structured graduate intake.",
  },
  {
    id: "c5", name: "Woolworths Group", slug: "woolworths-group", industry: "Retail",
    size: "10,000+ employees", location: "Bella Vista, NSW", verified: true, openRoles: 11,
    tags: ["Retail", "Data"],
    summary: "Retail group running one of the largest data platforms in Australia.",
  },
  {
    id: "c6", name: "Northbridge Analytics", slug: "northbridge-analytics", industry: "Consulting",
    size: "50-200 employees", location: "Melbourne, VIC", verified: false, openRoles: 3,
    tags: ["Consulting", "Data"],
    summary: "Boutique analytics consultancy working with mid-market clients.",
  },
];

const company = (id: string) => {
  const c = COMPANIES.find((x) => x.id === id)!;
  return { id: c.id, name: c.name, slug: c.slug, verified: c.verified };
};

export const JOBS: Job[] = [
  {
    id: "j1", slug: "junior-data-analyst-atlassian", title: "Junior Data Analyst",
    company: company("c1"), location: "Sydney, NSW", workMode: "Hybrid", workType: "Full-time",
    experience: "Graduate", compensation: "$75,000 - $85,000",
    summary:
      "Work with the analytics team on reporting pipelines and dashboards. Structured 12-week onboarding, mentor assigned from week one.",
    skills: ["SQL", "Python", "Power BI"], postedAt: "2026-09-09", closesAt: "2026-09-30",
    isInternship: false,
  },
  {
    id: "j2", slug: "graduate-software-engineer-canva", title: "Graduate Software Engineer",
    company: company("c2"), location: "Sydney, NSW", workMode: "Hybrid", workType: "Full-time",
    experience: "Graduate", compensation: "$88,000 + equity",
    summary:
      "Join a product team shipping to millions of users. Paired with a senior engineer for your first six months.",
    skills: ["TypeScript", "React", "Testing"], postedAt: "2026-09-07", closesAt: "2026-10-05",
    isInternship: false,
  },
  {
    id: "j3", slug: "graduate-registered-nurse-nsw-health", title: "Graduate Registered Nurse",
    company: company("c3"), location: "Western Sydney, NSW", workMode: "On-site", workType: "Full-time",
    experience: "Graduate", compensation: "$72,000 - $79,000",
    summary:
      "Twelve-month transition-to-practice program with rotations across medical, surgical and community care.",
    skills: ["Patient care", "Clinical handover", "AHPRA registration"],
    postedAt: "2026-09-05", closesAt: "2026-09-26", isInternship: false,
  },
  {
    id: "j4", slug: "business-analyst-graduate-cba", title: "Business Analyst - Graduate Program",
    company: company("c4"), location: "Sydney, NSW", workMode: "Hybrid", workType: "Full-time",
    experience: "Graduate", compensation: "$79,000 - $84,000",
    summary:
      "Rotate through three business units over 18 months, with a capstone project presented to the executive team.",
    skills: ["Requirements", "SQL", "Stakeholder management"],
    postedAt: "2026-09-02", closesAt: "2026-10-12", isInternship: false,
  },
  {
    id: "j5", slug: "software-engineering-intern-canva", title: "Software Engineering Intern",
    company: company("c2"), location: "Sydney, NSW", workMode: "Hybrid", workType: "Internship",
    experience: "No experience needed", compensation: "$38/hour",
    summary:
      "A paid twelve-week summer placement on a real product team, with a named mentor and a demo day at the end.",
    skills: ["TypeScript", "Git", "Problem solving"],
    postedAt: "2026-09-08", closesAt: "2026-09-29", isInternship: true,
    internship: { durationWeeks: 12, paid: true, cohortStart: "2026-11-23", convertsToGraduateRole: true },
  },
  {
    id: "j6", slug: "data-engineering-intern-woolworths", title: "Data Engineering Intern",
    company: company("c5"), location: "Bella Vista, NSW", workMode: "Hybrid", workType: "Internship",
    experience: "No experience needed", compensation: "$34/hour",
    summary:
      "Support the platform team building ingestion pipelines. Suited to a penultimate-year IT student.",
    skills: ["Python", "SQL", "Cloud basics"],
    postedAt: "2026-09-04", closesAt: "2026-10-02", isInternship: true,
    internship: { durationWeeks: 10, paid: true, cohortStart: "2026-12-01", convertsToGraduateRole: false },
  },
  {
    id: "j7", slug: "analytics-intern-northbridge", title: "Analytics Intern",
    company: company("c6"), location: "Melbourne, VIC", workMode: "Remote", workType: "Internship",
    experience: "No experience needed", compensation: "$32/hour",
    summary:
      "Remote-first placement supporting client reporting. Two days a week during semester, full-time over summer.",
    skills: ["Excel", "SQL", "Reporting"],
    postedAt: "2026-09-01", closesAt: "2026-09-25", isInternship: true,
    internship: { durationWeeks: 16, paid: true, cohortStart: "2026-10-19", convertsToGraduateRole: false },
  },
  {
    id: "j8", slug: "reporting-analyst-woolworths", title: "Reporting Analyst",
    company: company("c5"), location: "Bella Vista, NSW", workMode: "Hybrid", workType: "Full-time",
    experience: "1-2 years", compensation: "$85,000 - $92,000",
    summary:
      "Own a set of commercial dashboards end to end and work directly with category managers.",
    skills: ["Power BI", "SQL", "Stakeholder management"],
    postedAt: "2026-08-30", closesAt: "2026-09-27", isInternship: false,
  },
];

export const APPLICATIONS: Application[] = [
  {
    id: "a1", jobTitle: "Junior Data Analyst", companyName: "Atlassian",
    status: "Interview", updatedAt: "2026-09-12",
    stages: [
      { name: "Submitted", state: "done", date: "2 Sep" },
      { name: "In review", state: "done", date: "5 Sep" },
      { name: "Interview", state: "current", date: "12 Sep" },
      { name: "Offer", state: "todo" },
      { name: "Outcome", state: "todo" },
    ],
  },
  { id: "a2", jobTitle: "Graduate Software Engineer", companyName: "Canva", status: "InReview", updatedAt: "2026-09-08", stages: [] },
  { id: "a3", jobTitle: "Business Analyst - Graduate Program", companyName: "Commonwealth Bank", status: "Interview", updatedAt: "2026-09-06", stages: [] },
  { id: "a4", jobTitle: "Data Engineering Intern", companyName: "Woolworths Group", status: "Submitted", updatedAt: "2026-09-04", stages: [] },
  { id: "a5", jobTitle: "Analytics Cadet", companyName: "NSW Health", status: "Unsuccessful", updatedAt: "2026-08-28", stages: [] },
];

export const INTERVIEWS: Interview[] = [
  { id: "i1", jobTitle: "Business Analyst", companyName: "Commonwealth Bank", scheduledFor: "Thu 17 Sep - 10:00", mode: "Video", durationMinutes: 45 },
  { id: "i2", jobTitle: "Junior Data Analyst", companyName: "Atlassian", scheduledFor: "Tue 22 Sep - 14:30", mode: "On-site, Sydney", durationMinutes: 60 },
];

export const NOTIFICATIONS: AppNotification[] = [
  { id: "n1", message: "Commonwealth Bank moved you to Interview", at: "2 hours ago", tone: "success" },
  { id: "n2", message: "Cyber Security Analyst closes in 2 days", at: "Yesterday", tone: "warning" },
  { id: "n3", message: "Canva received your application", at: "3 days ago", tone: "info" },
];

export const PLATFORM_STATS = [
  { value: "471", label: "Live roles" },
  { value: "128", label: "Verified employers" },
  { value: "96%", label: "Applications get a status update" },
  { value: "4.2 days", label: "Median time to first response" },
];

export const JOURNEY = [
  { key: "discover", title: "Discover", applicant: "Search verified roles by field, location and work type.", recruiter: "Publish a role once and reach candidates who match it." },
  { key: "match", title: "Match", applicant: "See how your profile lines up against what each role asks for.", recruiter: "See which requirements each candidate actually meets." },
  { key: "prepare", title: "Prepare", applicant: "Role-specific prompts and checklists before you write a word.", recruiter: "Tell candidates what the process looks like up front." },
  { key: "apply", title: "Apply", applicant: "One profile, one resume, reusable across every application.", recruiter: "Structured applications instead of an inbox of attachments." },
  { key: "track", title: "Track", applicant: "Every stage and date in one timeline, updated by the employer.", recruiter: "Move candidates through stages in a single view." },
  { key: "interview", title: "Interview", applicant: "Times, formats and who you will meet, confirmed in advance.", recruiter: "Schedule and confirm without a separate calendar thread." },
  { key: "outcome", title: "Outcome", applicant: "A clear result either way, and feedback wherever it is given.", recruiter: "Close the loop on every candidate in one action." },
];
