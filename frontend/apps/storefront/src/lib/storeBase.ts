import { createContext, useCallback, useContext, useEffect } from 'react';
import type { PublicStore } from '@gentlestore/shared';
import { ON_PLATFORM_DOMAIN } from '../api';

/**
 * Where the current store's pages live: under "/{slug}" on the platform's domain, at the root of
 * its own domain. Provided by StoreLayout; every link to a store page goes through useStorePath.
 */
export interface StoreBase {
  slug: string;
  /** "/{slug}", or "" on the store's own domain. */
  prefix: string;
}

export const StoreBaseContext = createContext<StoreBase>({ slug: '', prefix: '' });

export function useStoreSlug(): string {
  return useContext(StoreBaseContext).slug;
}

/** Builds a link to a page of the current store: storePath('/cart'), storePath() for its front page. */
export function useStorePath(): (path?: string) => string {
  const { prefix } = useContext(StoreBaseContext);
  return useCallback((path = '') => prefix + path || '/', [prefix]);
}

/**
 * Sends people from a store's catalogue pages on the platform's domain (example.com/flowers/…) to
 * the same page on the store's own domain, once it has one, so there is a single copy to bookmark,
 * share and index.
 *
 * Only catalogue pages call this. Orders and invite links stay put, as do invite-only stores
 * altogether: the session cookie that lets a browser in belongs to the domain it was set on, so
 * moving those visitors would sign them out.
 */
export function useStoreDomainRedirect(store: PublicStore | undefined) {
  const { prefix } = useContext(StoreBaseContext);

  useEffect(() => {
    if (!ON_PLATFORM_DOMAIN || !store?.customDomain || store.accessMode !== 'Public') return;

    const { pathname, search } = window.location;
    const below = pathname.startsWith(prefix) ? pathname.slice(prefix.length) : '';
    window.location.replace(`https://${store.customDomain}${below || '/'}${search}`);
  }, [store, prefix]);
}
