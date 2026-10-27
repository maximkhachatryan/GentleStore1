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

/**
 * The copies the API keeps of every uploaded image (UploadImages.Sizes), by longest side:
 * sm 320px for thumbnails and logos, md 640px for product cards, lg 1600px for the product page.
 * Pick the smallest that stays sharp where the image is shown.
 */
export type ImageSize = 'sm' | 'md' | 'lg';

// An image as the API stores it. Anything else — a pasted link, demo data — has no sized copies.
const UPLOAD_PATH = /^\/uploads\/([a-f0-9]{32})\.[a-z]+$/;

export function resolveImageUrl(baseUrl: string, url: string | null | undefined, size: ImageSize): string | undefined {
  const upload = url ? UPLOAD_PATH.exec(url) : null;
  return resolveAssetUrl(baseUrl, upload ? `/uploads/${size}/${upload[1]}.webp` : url);
}
