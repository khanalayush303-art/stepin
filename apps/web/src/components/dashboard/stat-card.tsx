import type { LucideIcon } from "lucide-react";
import { Card } from "@/components/ui/card";
import { cn } from "@/lib/utils";

export interface StatCardProps {
  icon: LucideIcon;
  label: string;
  value: string;
  /** Always includes a direction word, never an arrow or colour alone. */
  delta?: string;
  deltaTone?: "success" | "info" | "warning" | "brand";
}

const TONE = {
  success: "text-success-subtle-fg",
  info: "text-info-subtle-fg",
  warning: "text-warning-subtle-fg",
  brand: "text-primary",
} as const;

export function StatCard({ icon: Icon, label, value, delta, deltaTone = "info" }: StatCardProps) {
  return (
    <Card className="flex flex-col gap-2 p-6">
      <div className="flex items-center gap-2">
        <Icon className="size-[18px] text-muted-foreground" aria-hidden="true" />
        <span className="text-caption text-muted-foreground">{label}</span>
      </div>
      <p className="text-h1 text-foreground">{value}</p>
      {delta ? <p className={cn("text-caption", TONE[deltaTone])}>{delta}</p> : null}
    </Card>
  );
}
