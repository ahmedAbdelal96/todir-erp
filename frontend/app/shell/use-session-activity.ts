import { useCallback, useEffect, useRef, useState } from "react";
import { ApiError } from "../../core/api/http-client.ts";
import { recordSessionActivity, type SessionActivityResponse } from "../../core/api/auth-service.ts";

const WARNING_WINDOW_MS = 2 * 60 * 1000;
const ACTIVITY_WRITE_INTERVAL_MS = 60 * 1000;

export interface SessionTiming {
  readonly inactivityExpiresAt: string;
  readonly reauthenticationExpiresAt: string;
}

export function useSessionActivity(
  timing: SessionTiming | null,
  onExpired: () => void
): { readonly warningOpen: boolean; readonly continueSession: () => Promise<void>; readonly expiresAt: string | null } {
  const [warningOpen, setWarningOpen] = useState(false);
  const [activeTiming, setActiveTiming] = useState<SessionTiming | null>(timing);
  const lastSentAt = useRef(0);
  const inFlight = useRef<Promise<void> | null>(null);
  const onExpiredRef = useRef(onExpired);

  useEffect(() => { onExpiredRef.current = onExpired; }, [onExpired]);

  useEffect(() => {
    setActiveTiming(timing);
    setWarningOpen(false);
  }, [timing?.inactivityExpiresAt, timing?.reauthenticationExpiresAt]);

  const applyActivity = useCallback((response: SessionActivityResponse) => {
    setActiveTiming({ inactivityExpiresAt: response.inactivityExpiresAt, reauthenticationExpiresAt: response.reauthenticationExpiresAt });
    setWarningOpen(false);
  }, []);

  const sendActivity = useCallback(async (force: boolean) => {
    if (!activeTiming || inFlight.current || (!force && Date.now() - lastSentAt.current < ACTIVITY_WRITE_INTERVAL_MS)) return;
    lastSentAt.current = Date.now();
    const request = recordSessionActivity()
      .then(applyActivity)
      .catch((error: unknown) => {
        if (error instanceof ApiError && error.status === 401) onExpiredRef.current();
      })
      .finally(() => { inFlight.current = null; });
    inFlight.current = request;
    await request;
  }, [activeTiming, applyActivity]);

  useEffect(() => {
    if (!activeTiming) return;
    const markMeaningfulActivity = () => { void sendActivity(false); };
    document.addEventListener("pointerdown", markMeaningfulActivity, { passive: true });
    document.addEventListener("keydown", markMeaningfulActivity, { passive: true });
    document.addEventListener("touchstart", markMeaningfulActivity, { passive: true });
    window.addEventListener("focus", markMeaningfulActivity);
    return () => {
      document.removeEventListener("pointerdown", markMeaningfulActivity);
      document.removeEventListener("keydown", markMeaningfulActivity);
      document.removeEventListener("touchstart", markMeaningfulActivity);
      window.removeEventListener("focus", markMeaningfulActivity);
    };
  }, [activeTiming, sendActivity]);

  useEffect(() => {
    if (!activeTiming) return;
    const inactivityAt = Date.parse(activeTiming.inactivityExpiresAt);
    const reauthenticationAt = Date.parse(activeTiming.reauthenticationExpiresAt);
    const warningAt = Math.min(inactivityAt - WARNING_WINDOW_MS, reauthenticationAt);
    const now = Date.now();
    const warningTimer = window.setTimeout(() => setWarningOpen(true), Math.max(0, warningAt - now));
    const expiryTimer = window.setTimeout(() => onExpiredRef.current(), Math.max(0, Math.min(inactivityAt, reauthenticationAt) - now));
    return () => { window.clearTimeout(warningTimer); window.clearTimeout(expiryTimer); };
  }, [activeTiming]);

  const continueSession = useCallback(async () => { await sendActivity(true); }, [sendActivity]);
  return { warningOpen, continueSession, expiresAt: activeTiming?.inactivityExpiresAt ?? null };
}
