import axios from 'axios';
import type { RedeemInviteStatus } from '@gentlestore/shared';

const INVITE_REQUIRED = 'invite_required';

/** True when the API refused because this browser has not redeemed an invite for the store. */
export function isInviteRequired(error: unknown): boolean {
  if (!axios.isAxiosError(error)) return false;
  const data = error.response?.data as { code?: string } | undefined;
  return error.response?.status === 403 && data?.code === INVITE_REQUIRED;
}

/** Why the gate screen is showing. Drives the copy, not the layout. */
export type GateReason = RedeemInviteStatus | 'locked' | 'rate_limited' | 'error';

/**
 * Turns a failed redeem into the reason to explain. The API answers every refusal with a
 * `status` field, so unexpected shapes fall back to a generic message rather than a blank screen.
 */
export function redeemFailureReason(error: unknown): GateReason {
  if (!axios.isAxiosError(error)) return 'error';
  if (error.response?.status === 429) return 'rate_limited';

  const status = (error.response?.data as { status?: RedeemInviteStatus } | undefined)?.status;
  return status ?? 'error';
}

/**
 * Invite secrets travel in the URL fragment (`#i=…`) because fragments are never sent to a
 * server — they stay out of proxy logs, Referer headers and analytics. The `?i=` fallback covers
 * clients that drop fragments while linkifying a message.
 */
export function readInviteToken(): string | null {
  const hash = window.location.hash.replace(/^#/, '');
  return new URLSearchParams(hash).get('i') ?? new URLSearchParams(window.location.search).get('i');
}

/** Drops the secret from the address bar, browser history and any screenshot taken later. */
export function scrubInviteToken(): void {
  window.history.replaceState(null, '', window.location.pathname);
}
