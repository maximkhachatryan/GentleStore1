/**
 * The fonts a store's theme can pick, by the keys the API uses (backend Theming/ThemeCatalog.cs —
 * keep the two lists in step). Each is self-hosted and only downloaded once something uses it (a
 * themed storefront, the console's theme preview); the browser then fetches just the character
 * subsets a page actually shows.
 *
 * Every face comes with the Armenian one its family stack falls back on, so Armenian text keeps a
 * matching style even where the display face has no Armenian letters.
 */
const sansArmenian = () => import('@fontsource-variable/noto-sans-armenian');
const serifArmenian = () => import('@fontsource-variable/noto-serif-armenian');

const FONT_LOADERS: Record<string, Array<() => Promise<unknown>>> = {
  inter: [() => import('@fontsource-variable/inter'), sansArmenian],
  montserrat: [() => import('@fontsource-variable/montserrat'), sansArmenian],
  roboto: [() => import('@fontsource-variable/roboto'), sansArmenian],
  'noto-sans': [() => import('@fontsource-variable/noto-sans'), sansArmenian],
  playfair: [() => import('@fontsource-variable/playfair-display'), serifArmenian],
  lora: [() => import('@fontsource-variable/lora'), serifArmenian],
  'noto-serif': [() => import('@fontsource-variable/noto-serif'), serifArmenian],
};

/** Adds the @font-face rules for these fonts; each is fetched once however often it is asked for. */
export function loadFonts(keys: string[]) {
  for (const key of keys) {
    for (const load of FONT_LOADERS[key] ?? []) {
      // A font that fails to load leaves its stack's next face in place; nothing to report.
      load().catch(() => undefined);
    }
  }
}
