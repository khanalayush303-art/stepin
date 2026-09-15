"use client";

import * as React from "react";
import { getCurrentUser } from "./api";
import type { AuthUser } from "./types";

type SessionStatus = "loading" | "authenticated" | "unauthenticated";

interface SessionState {
  status: SessionStatus;
  user: AuthUser | null;
  /** Re-fetches /me — call after login, account-setup, or logout. */
  refresh: () => Promise<void>;
}

const SessionContext = React.createContext<SessionState | undefined>(undefined);

/** Asks the backend who's signed in via the HttpOnly session cookie — no localStorage fallback. */
export function SessionProvider({ children }: { children: React.ReactNode }) {
  const [status, setStatus] = React.useState<SessionStatus>("loading");
  const [user, setUser] = React.useState<AuthUser | null>(null);

  const refresh = React.useCallback(async () => {
    try {
      const current = await getCurrentUser();
      setUser(current);
      setStatus("authenticated");
    } catch {
      setUser(null);
      setStatus("unauthenticated");
    }
  }, []);

  React.useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect -- setState is inside refresh()'s async continuation, not the effect body
    void refresh();
  }, [refresh]);

  const value = React.useMemo(() => ({ status, user, refresh }), [status, user, refresh]);

  return <SessionContext.Provider value={value}>{children}</SessionContext.Provider>;
}

export function useSession(): SessionState {
  const context = React.useContext(SessionContext);
  if (!context) {
    throw new Error("useSession must be used within a SessionProvider");
  }
  return context;
}
