import { useEffect } from 'react';

/**
 * Keeps the tab title in step as people move around inside the app. The first page load already
 * has the right one, put there by nginx from the API (backend Seo/PageHead.cs) — so the format
 * must match: "{Store}", "{Category} · {Store}", "{Product} · {Store}".
 * Nothing happens until the title is known, so a loading page keeps the one it has.
 */
export function usePageTitle(...parts: (string | null | undefined)[]) {
  const title = parts.every(Boolean) ? parts.join(' · ') : null;

  useEffect(() => {
    if (title) document.title = title;
  }, [title]);
}
