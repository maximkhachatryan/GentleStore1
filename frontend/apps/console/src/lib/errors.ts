import axios from 'axios';

/** Reads the machine-readable `code` the API attaches to validation and conflict responses. */
export function apiErrorCode(error: unknown): string | undefined {
  return apiErrorPayload<{ code?: string }>(error)?.code;
}

/** The response body of a failed request, for the cases that carry data worth acting on. */
export function apiErrorPayload<T>(error: unknown): T | undefined {
  if (!axios.isAxiosError(error)) return undefined;
  return error.response?.data as T | undefined;
}
