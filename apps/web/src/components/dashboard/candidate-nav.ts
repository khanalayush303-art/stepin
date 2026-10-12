import { Bell, Bookmark, Compass, FileText, FlaskConical, FolderGit2, GraduationCap, LayoutDashboard, Search, Target, User } from "lucide-react";
import type { DashboardNavItem } from "./dashboard-shell";

/**
 * The one candidate dashboard nav. Every candidate page imports this instead of keeping its own
 * copy — a lesson learned the hard way: adding a feature used to mean editing every page's own
 * duplicated array, and it was easy to miss one (Job Matches shipped, then Research shipped
 * without ever being added back to the Matches page's own list). One array, no drift.
 */
export const CANDIDATE_NAV: DashboardNavItem[] = [
  { href: "/dashboard", label: "Overview", icon: LayoutDashboard },
  { href: "/dashboard/discover", label: "Discover roles", icon: Search },
  { href: "/dashboard/matches", label: "Job matches", icon: Target },
  { href: "/dashboard/research", label: "Research", icon: FlaskConical },
  { href: "/dashboard/career-gps", label: "Career GPS", icon: Compass },
  { href: "/dashboard/portfolio", label: "Portfolio", icon: FolderGit2 },
  { href: "/dashboard/applications", label: "Applications", icon: FileText, badge: "12" },
  { href: "/dashboard/saved", label: "Saved roles", icon: Bookmark, badge: "8" },
  { href: "/dashboard/interviews", label: "Interviews", icon: User, badge: "2" },
  { href: "/dashboard/prepare", label: "Prepare", icon: GraduationCap },
  { href: "/dashboard/notifications", label: "Notifications", icon: Bell, badge: "3" },
];
