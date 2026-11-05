import { Outlet, useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { CartProvider } from '../lib/cart';
import { StoreBaseContext } from '../lib/storeBase';
import { useStoreTheme } from '../lib/theme';
import { api } from '../api';

/**
 * Gives every page of a store its slug, its link prefix, a cart scoped to it and its look. The
 * store comes from the URL (/:slug) on the platform's domain, or from the domain itself (hostSlug).
 */
export default function StoreLayout({ hostSlug }: { hostSlug?: string }) {
  const params = useParams();
  const slug = hostSlug ?? params.slug ?? '';

  // Every store page loads the store itself; this only watches for it, so a locked store's 403
  // (or a page still loading) is not asked for twice.
  const { data: store } = useQuery({
    queryKey: ['store', slug],
    queryFn: () => api.store.get(slug),
    enabled: false,
  });
  useStoreTheme(store ? store.theme : undefined, store?.logoUrl);

  return (
    <StoreBaseContext.Provider value={{ slug, prefix: hostSlug ? '' : `/${slug}` }}>
      <CartProvider slug={slug}>
        <Outlet />
      </CartProvider>
    </StoreBaseContext.Provider>
  );
}
