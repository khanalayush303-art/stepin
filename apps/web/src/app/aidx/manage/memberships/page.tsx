"use client";

import * as React from "react";
import { useAuth } from "@clerk/nextjs";
import { Alert } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import { AidxSearchSelect } from "@/components/aidx/aidx-search-select";
import { listResearchAreaOptions } from "@/lib/aidx/cms";
import {
  grantMembership,
  listMemberships,
  revokeMembership,
  searchUsers,
  updateMembership,
  type AidxMembership,
} from "@/lib/aidx/memberships";
import type { ResearchArea } from "@/lib/aidx/types";
import { formError } from "@/lib/auth/client";

const ROLES = ["Contributor", "Editor", "LabManager"] as const;

export default function AidxMembershipsPage() {
  const { getToken } = useAuth();
  const [memberships, setMemberships] = React.useState<AidxMembership[]>();
  const [areas, setAreas] = React.useState<ResearchArea[]>([]);
  const [loading, setLoading] = React.useState(true);
  const [loadError, setLoadError] = React.useState<string | null>(null);
  const [actionError, setActionError] = React.useState<string | null>(null);
  const [actionNotice, setActionNotice] = React.useState<string | null>(null);
  const [busyId, setBusyId] = React.useState<string | null>(null);

  const [userQuery, setUserQuery] = React.useState("");
  const [userOptions, setUserOptions] = React.useState<{ id: string; label: string; hint?: string }[]>([]);
  const [selectedUser, setSelectedUser] = React.useState<{ id: string; label: string } | null>(null);
  const [grantRole, setGrantRole] = React.useState<string>("Contributor");
  const [grantScope, setGrantScope] = React.useState<string>("");
  const [granting, setGranting] = React.useState(false);

  const load = React.useCallback(async () => {
    setLoading(true);
    setLoadError(null);
    try {
      const token = await getToken();
      const [membershipList, areaList] = await Promise.all([
        listMemberships(token),
        listResearchAreaOptions().catch(() => [] as ResearchArea[]),
      ]);
      setMemberships(membershipList);
      setAreas(areaList);
    } catch (caught) {
      setLoadError(formError(caught));
    } finally {
      setLoading(false);
    }
  }, [getToken]);

  React.useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void load();
  }, [load]);

  React.useEffect(() => {
    if (userQuery.trim().length < 2) {
      return;
    }
    let cancelled = false;
    const timeout = setTimeout(async () => {
      try {
        const token = await getToken();
        const results = await searchUsers(token, userQuery.trim());
        if (!cancelled) {
          setUserOptions(results.map((r) => ({ id: r.id, label: `${r.firstName} ${r.lastName}`, hint: r.email })));
        }
      } catch {
        if (!cancelled) setUserOptions([]);
      }
    }, 300);
    return () => {
      cancelled = true;
      clearTimeout(timeout);
    };
  }, [userQuery, getToken]);

  const effectiveUserOptions = userQuery.trim().length < 2 ? [] : userOptions;

  function areaName(id: string | null) {
    if (!id) return null;
    return areas.find((a) => a.id === id)?.name ?? "Unknown area";
  }

  async function onGrant() {
    if (!selectedUser) {
      setActionError("Search for and select a person first.");
      return;
    }
    setGranting(true);
    setActionError(null);
    setActionNotice(null);
    try {
      const token = await getToken();
      await grantMembership(token, selectedUser.id, grantRole, grantScope || null);
      setActionNotice(`Granted ${grantRole} to ${selectedUser.label}.`);
      setSelectedUser(null);
      setUserQuery("");
      setGrantRole("Contributor");
      setGrantScope("");
      await load();
    } catch (caught) {
      setActionError(formError(caught));
    } finally {
      setGranting(false);
    }
  }

  async function onRoleChange(membership: AidxMembership, role: string) {
    setBusyId(membership.id);
    setActionError(null);
    try {
      const token = await getToken();
      await updateMembership(token, membership.id, role, membership.scopedResearchAreaId);
      await load();
    } catch (caught) {
      setActionError(formError(caught));
    } finally {
      setBusyId(null);
    }
  }

  async function onScopeChange(membership: AidxMembership, scope: string | null) {
    setBusyId(membership.id);
    setActionError(null);
    try {
      const token = await getToken();
      await updateMembership(token, membership.id, membership.role, scope);
      await load();
    } catch (caught) {
      setActionError(formError(caught));
    } finally {
      setBusyId(null);
    }
  }

  async function onRevoke(membership: AidxMembership) {
    setBusyId(membership.id);
    setActionError(null);
    setActionNotice(null);
    try {
      const token = await getToken();
      await revokeMembership(token, membership.id);
      setActionNotice(`Revoked ${membership.userEmail}'s membership.`);
      await load();
    } catch (caught) {
      setActionError(formError(caught));
    } finally {
      setBusyId(null);
    }
  }

  if (loading) {
    return (
      <div className="container-page space-y-6 py-10">
        <Skeleton className="h-10 w-64" />
        <Skeleton className="h-40 w-full" />
      </div>
    );
  }

  if (loadError) {
    return (
      <div className="container-page py-10">
        <Alert tone="error" title="Couldn't load memberships">
          {loadError}
        </Alert>
      </div>
    );
  }

  return (
    <div className="container-page max-w-4xl space-y-8 py-10">
      <div className="space-y-1">
        <h1 className="text-h2 text-foreground">AIDX lab memberships</h1>
        <p className="text-body text-muted-foreground">
          Grant or change a Contributor/Editor/LabManager role, optionally scoped to one research area.
          Revoking never deletes history — it only withdraws access immediately.
        </p>
      </div>

      {actionNotice ? (
        <Alert tone="success" title="Done">
          {actionNotice}
        </Alert>
      ) : null}
      {actionError ? (
        <Alert tone="error" title="That action didn't go through">
          {actionError}
        </Alert>
      ) : null}

      <Card className="space-y-4 p-5">
        <h2 className="text-h4 text-foreground">Grant a membership</h2>
        <div className="grid gap-4 sm:grid-cols-[2fr_1fr_1fr_auto] sm:items-end">
          <div>
            {selectedUser ? (
              <div className="flex items-center justify-between rounded-md border border-border p-2">
                <span className="text-small text-foreground">{selectedUser.label}</span>
                <Button variant="ghost" size="sm" onClick={() => setSelectedUser(null)}>
                  Change
                </Button>
              </div>
            ) : (
              <AidxSearchSelect
                options={effectiveUserOptions}
                placeholder="Search by name or email…"
                onQueryChange={setUserQuery}
                onSelect={(option) => {
                  setSelectedUser({ id: option.id, label: option.label });
                  setUserQuery(option.label);
                }}
                emptyText={userQuery.trim().length < 2 ? "Type at least 2 characters…" : "No matching users."}
              />
            )}
          </div>
          <Select value={grantRole} onValueChange={setGrantRole}>
            <SelectTrigger aria-label="Role">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {ROLES.map((role) => (
                <SelectItem key={role} value={role}>
                  {role}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          <Select value={grantScope || "__none"} onValueChange={(v) => setGrantScope(v === "__none" ? "" : v)}>
            <SelectTrigger aria-label="Scope">
              <SelectValue placeholder="Lab-wide" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="__none">Lab-wide</SelectItem>
              {areas.map((area) => (
                <SelectItem key={area.id} value={area.id}>
                  {area.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          <Button onClick={onGrant} loading={granting}>
            Grant
          </Button>
        </div>
      </Card>

      <Card className="divide-y divide-border p-0">
        {memberships && memberships.length > 0 ? (
          memberships.map((membership) => (
            <div key={membership.id} className="flex flex-wrap items-center gap-3 p-4">
              <div className="min-w-0 flex-1">
                <p className="text-small font-medium text-foreground">{membership.userEmail}</p>
                <p className="text-caption text-muted-foreground">
                  {areaName(membership.scopedResearchAreaId) ?? "Lab-wide"}
                  {membership.status === "Revoked" ? " · revoked" : ""}
                </p>
              </div>
              {membership.status === "Active" ? (
                <>
                  <Select value={membership.role} onValueChange={(role) => onRoleChange(membership, role)}>
                    <SelectTrigger aria-label={`Role for ${membership.userEmail}`} className="w-40">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {ROLES.map((role) => (
                        <SelectItem key={role} value={role}>
                          {role}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  <Select
                    value={membership.scopedResearchAreaId ?? "__none"}
                    onValueChange={(v) => onScopeChange(membership, v === "__none" ? null : v)}
                  >
                    <SelectTrigger aria-label={`Scope for ${membership.userEmail}`} className="w-40">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="__none">Lab-wide</SelectItem>
                      {areas.map((area) => (
                        <SelectItem key={area.id} value={area.id}>
                          {area.name}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  <Button variant="outline" size="sm" loading={busyId === membership.id} onClick={() => onRevoke(membership)}>
                    Revoke
                  </Button>
                </>
              ) : (
                <Badge tone="neutral" variant="subtle">
                  {membership.role}
                </Badge>
              )}
            </div>
          ))
        ) : (
          <p className="p-4 text-small text-muted-foreground">No memberships granted yet.</p>
        )}
      </Card>
    </div>
  );
}
