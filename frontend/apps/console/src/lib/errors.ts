import axios from 'axios';

/** Reads the machine-readable `code` the API attaches to validation and conflict responses. */
export function apiErrorCode(error: unknown): string | undefined {
  if (!axios.isAxiosError(error)) return undefined;
  return (error.response?.data as { code?: string } | undefined)?.code;
}
