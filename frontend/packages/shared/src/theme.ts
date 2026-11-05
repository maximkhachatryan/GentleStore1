/**
 * Colour arithmetic for store themes. The API makes the same choice for the storefront
 * (backend Theming/ThemeCatalog.OnColor); this copy lets the console preview it before saving.
 */

const DARK_TEXT = '#111827';

function luminance(hex: string): number {
  const channel = (at: number) => {
    const c = parseInt(hex.slice(at, at + 2), 16) / 255;
    return c <= 0.03928 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4;
  };
  return 0.2126 * channel(1) + 0.7152 * channel(3) + 0.0722 * channel(5);
}

/** WCAG contrast ratio of two "#rrggbb" colours, from 1 (none) to 21. */
export function contrastRatio(a: string, b: string): number {
  const [hi, lo] = [luminance(a), luminance(b)].sort((x, y) => y - x);
  return (hi + 0.05) / (lo + 0.05);
}

/** White or near-black text, whichever reads better on the colour. */
export function onBrandColor(brand: string): string {
  return contrastRatio(brand, '#ffffff') >= contrastRatio(brand, DARK_TEXT) ? '#ffffff' : DARK_TEXT;
}
