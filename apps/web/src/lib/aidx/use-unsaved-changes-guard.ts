"use client";

import * as React from "react";

const DEFAULT_MESSAGE = "You have unsaved changes. Leave this page without saving?";

/**
 * Warns before losing unsaved edits, two ways:
 *  - `beforeunload`: browser close/refresh/address-bar navigation.
 *  - A capture-phase click listener on same-tab internal <a>/<Link> clicks (breadcrumbs, "Cancel",
 *    "Back to X" buttons): intercepted with a confirm() before the navigation is allowed through.
 *
 * Deliberately does not intercept programmatic `router.push`: every AIDX form only calls that after
 * a successful save, a confirmed delete, or a just-created record — i.e. never while `dirty` is
 * still true. If a future caller adds a push while dirty, this hook won't catch it; that's a real,
 * narrow boundary rather than a false guarantee of catching every navigation path.
 */
export function useUnsavedChangesGuard(dirty: boolean, message: string = DEFAULT_MESSAGE) {
  React.useEffect(() => {
    if (!dirty) return;

    const onBeforeUnload = (event: BeforeUnloadEvent) => {
      event.preventDefault();
      event.returnValue = "";
    };
    window.addEventListener("beforeunload", onBeforeUnload);
    return () => window.removeEventListener("beforeunload", onBeforeUnload);
  }, [dirty]);

  React.useEffect(() => {
    if (!dirty) return;

    const onClick = (event: MouseEvent) => {
      if (event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) {
        return;
      }
      const anchor = (event.target as HTMLElement | null)?.closest("a");
      if (!anchor) return;
      const href = anchor.getAttribute("href");
      if (!href || href.startsWith("#") || href.startsWith("http") || href.startsWith("mailto:")) return;
      if (anchor.target && anchor.target !== "_self") return;

      if (!window.confirm(message)) {
        event.preventDefault();
        event.stopPropagation();
      }
    };

    document.addEventListener("click", onClick, true);
    return () => document.removeEventListener("click", onClick, true);
  }, [dirty, message]);
}
