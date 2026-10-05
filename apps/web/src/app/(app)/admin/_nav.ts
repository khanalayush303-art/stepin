import {
  BookOpen,
  Building2,
  FolderKanban,
  FileText,
  GraduationCap,
  Library,
  Newspaper,
  CalendarDays,
  LayoutDashboard,
  Lightbulb,
  ScrollText,
  ShieldCheck,
  TrendingUp,
  Users,
} from "lucide-react";
import type { DashboardNavItem } from "@/components/dashboard/dashboard-shell";

/** Shared admin navigation. Every admin page uses this list so the sidebar stays consistent. */
export const ADMIN_NAV: DashboardNavItem[] = [
  { href: "/admin", label: "Overview", icon: LayoutDashboard },
  { href: "/admin/users", label: "Users", icon: Users },
  { href: "/admin/companies", label: "Companies", icon: Building2 },
  { href: "/admin/jobs", label: "Jobs", icon: FileText },
  { href: "/admin/aidx/research", label: "AIDX research", icon: BookOpen },
  { href: "/admin/aidx/projects", label: "AIDX projects", icon: FolderKanban },
  { href: "/admin/aidx/people", label: "AIDX people", icon: GraduationCap },
  { href: "/admin/aidx/publications", label: "AIDX publications", icon: Library },
  { href: "/admin/aidx/news", label: "AIDX news", icon: Newspaper },
  { href: "/admin/aidx/events", label: "AIDX events", icon: CalendarDays },
  { href: "/admin/aidx/opportunities", label: "AIDX opportunities", icon: Lightbulb },
  { href: "/admin/verification", label: "Verification", icon: ShieldCheck, badge: "7" },
  { href: "/admin/reports", label: "Reports", icon: TrendingUp },
  { href: "/admin/audit", label: "Audit log", icon: ScrollText },
];
