import { useAuth } from '../auth/AuthContext';
import { useManagedStore } from '../auth/managedStore';
import { STOREFRONT_URL } from '../api';

/** Public storefront URL of the store being worked on (own store, or the one an admin manages). */
export function useStorefrontBase(): string | null {
  const { user } = useAuth();
  const managed = useManagedStore();

  const slug = user?.role === 'SuperAdmin' ? managed?.slug : user?.storeSlug;
  return slug ? `${STOREFRONT_URL.replace(/\/$/, '')}/${slug}` : null;
}
