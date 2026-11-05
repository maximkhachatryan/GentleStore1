import { useEffect } from 'react';
import { loadFonts, resolveImageUrl, type StoreTheme } from '@gentlestore/shared';
import { API_URL } from '../api';

/** The page head's copy of the theme (backend Seo/PageHead.cs), there for the first paint. */
const HEAD_STYLE_ID = 'gs-theme';
const DEFAULT_THEME_COLOR = '#059669';

/**
 * Puts a store's look on the page while its pages are shown: the --gs-* variables index.css reads,
 * its fonts, the browser-bar colour and its logo as the tab icon. Undone on the way out, so the
 * platform's own pages keep the platform's look.
 *
 * @param theme The store's theme: null for none, undefined while the store has not loaded (the
 *   head's copy then stays in charge).
 */
export function useStoreTheme(theme: StoreTheme | null | undefined, logoUrl: string | null | undefined) {
  useEffect(() => {
    if (theme === undefined) return;

    // From here on the app owns the theme; the head's copy would outlive the store's pages.
    document.getElementById(HEAD_STYLE_ID)?.remove();
    if (!theme) return;

    const root = document.documentElement;
    const names = Object.keys(theme.cssVariables);
    for (const name of names) root.style.setProperty(name, theme.cssVariables[name]);
    loadFonts(theme.fonts);
    setThemeColor(theme.brandColor);

    return () => {
      for (const name of names) root.style.removeProperty(name);
      setThemeColor(DEFAULT_THEME_COLOR);
    };
  }, [theme]);

  // The tab icon follows the logo whether or not the store has a theme.
  useEffect(() => {
    const logo = resolveImageUrl(API_URL, logoUrl, 'sm');
    if (!logo) return;

    const icon = document.querySelector<HTMLLinkElement>('link[rel="icon"]');
    if (!icon) return;
    const original = { href: icon.href, type: icon.type };
    icon.type = '';
    icon.href = logo;
    return () => {
      icon.href = original.href;
      icon.type = original.type;
    };
  }, [logoUrl]);
}

/**
 * Whether the store is shown by its logo alone, with no name beside it: for a logo that already
 * spells the name out. A store without a logo always shows its name, or it would show nothing.
 */
export function showsLogoOnly(store: { logoUrl: string | null; theme?: StoreTheme | null } | undefined): boolean {
  return !!store?.logoUrl && store.theme?.showStoreName === false;
}

function setThemeColor(color: string) {
  // The head can carry two: index.html's default and the API's per-store one. Keep them in step.
  document.querySelectorAll<HTMLMetaElement>('meta[name="theme-color"]').forEach((m) => (m.content = color));
}
