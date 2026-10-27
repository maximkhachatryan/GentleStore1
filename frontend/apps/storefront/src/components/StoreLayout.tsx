import { Outlet, useParams } from 'react-router-dom';
import { CartProvider } from '../lib/cart';
import { StoreBaseContext } from '../lib/storeBase';

/**
 * Gives every page of a store its slug, its link prefix and a cart scoped to it. The store comes
 * from the URL (/:slug) on the platform's domain, or from the domain itself (hostSlug).
 */
export default function StoreLayout({ hostSlug }: { hostSlug?: string }) {
  const params = useParams();
  const slug = hostSlug ?? params.slug ?? '';

  return (
    <StoreBaseContext.Provider value={{ slug, prefix: hostSlug ? '' : `/${slug}` }}>
      <CartProvider slug={slug}>
        <Outlet />
      </CartProvider>
    </StoreBaseContext.Provider>
  );
}
