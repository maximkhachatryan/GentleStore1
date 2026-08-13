export * from './types';
export * from './token';
export * from './http';
export * from './api';
export * from './contact';

export function resolveAssetUrl(baseUrl: string, url: string | null | undefined): string | undefined {
  if (!url) return undefined;
  if (/^https?:\/\//i.test(url)) return url;
  return `${baseUrl.replace(/\/$/, '')}${url}`;
}
