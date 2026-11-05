/** "https://www.flowers.am/" → "flowers.am": how a store's own website is named in its shop. */
export function websiteLabel(url: string): string {
  try {
    return new URL(url).hostname.replace(/^www\./, '');
  } catch {
    return url;
  }
}
