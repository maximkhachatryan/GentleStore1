import { useAuth } from '../auth/AuthContext';
import { useManagedStore } from '../auth/managedStore';
import { STOREFRONT_URL } from '../api';

/**
 * Where a store's storefront lives: the root of its own domain when it has one, else /{slug} on the
 * platform's. Mirrors StorefrontLinks.StoreBase in the API.
 */
export function storefrontBaseFor(store: { slug: string; customDomain?: string | null }): string {
  return store.customDomain
    ? `https://${store.customDomain}`
    : `${STOREFRONT_URL.replace(/\/$/, '')}/${store.slug}`;
}

/** Public storefront URL of the store being worked on (own store, or the one an admin manages). */
export function useStorefrontBase(): string | null {
  const { user } = useAuth();
  const managed = useManagedStore();

  const store =
    user?.role === 'SuperAdmin'
      ? managed
      : user?.storeSlug
        ? { slug: user.storeSlug, customDomain: user.storeCustomDomain }
        : null;
  return store ? storefrontBaseFor(store) : null;
}
