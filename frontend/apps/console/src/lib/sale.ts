const pad = (n: number) => String(n).padStart(2, '0');

/**
 * A date input's "YYYY-MM-DD" → the last second of that day in the merchant's own time zone, so
 * "last day of the sale: 30 Nov" really includes all of 30 November.
 */
export function endOfDayIso(date: string): string {
  const [y, m, d] = date.split('-').map(Number);
  return new Date(y, m - 1, d, 23, 59, 59).toISOString();
}

/** An ISO instant → "YYYY-MM-DD" in local time, for a date input. */
export function toDateInput(iso: string | null | undefined): string {
  if (!iso) return '';
  const d = new Date(iso);
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}

export const todayInput = () => toDateInput(new Date().toISOString());

/** Mirrors the server: a sale with no end date runs until it is ended by hand. */
export function isSaleRunning(saleEndsAt: string | null): boolean {
  return !saleEndsAt || new Date(saleEndsAt).getTime() > Date.now();
}
