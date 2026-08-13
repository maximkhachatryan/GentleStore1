export function formatDateTime(value: string | null | undefined, locale: string): string {
  if (!value) return '—';
  return new Date(value).toLocaleString(locale, {
    dateStyle: 'medium',
    timeStyle: 'short',
  });
}

export function formatDate(value: string | null | undefined, locale: string): string {
  if (!value) return '—';
  return new Date(value).toLocaleDateString(locale, { dateStyle: 'medium' });
}
