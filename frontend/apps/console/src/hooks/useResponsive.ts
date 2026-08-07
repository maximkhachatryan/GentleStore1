import { useCallback, useSyncExternalStore } from 'react';

// Mirrors antd's md / lg breakpoints so component-level and CSS-level rules stay in sync.
const MOBILE_QUERY = '(max-width: 767px)';
const DESKTOP_QUERY = '(min-width: 992px)';

const lists = new Map<string, MediaQueryList>();

function mediaQueryList(query: string): MediaQueryList {
  let mql = lists.get(query);
  if (!mql) {
    mql = window.matchMedia(query);
    lists.set(query, mql);
  }
  return mql;
}

function useMediaQuery(query: string): boolean {
  const subscribe = useCallback(
    (onChange: () => void) => {
      const mql = mediaQueryList(query);
      mql.addEventListener('change', onChange);
      return () => mql.removeEventListener('change', onChange);
    },
    [query],
  );

  return useSyncExternalStore(
    subscribe,
    () => mediaQueryList(query).matches,
    () => false,
  );
}

export interface Responsive {
  /** Below 768px — phones. Data switches from tables to cards, dialogs to sheets. */
  isMobile: boolean;
  /** 992px and up — the persistent sidebar fits. */
  isDesktop: boolean;
}

export function useResponsive(): Responsive {
  return {
    isMobile: useMediaQuery(MOBILE_QUERY),
    isDesktop: useMediaQuery(DESKTOP_QUERY),
  };
}
