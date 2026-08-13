import { Link } from 'react-router-dom';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { resolveAssetUrl } from '@gentlestore/shared';
import LanguageSwitcher from './LanguageSwitcher';
import { useOptionalCart } from '../lib/cart';
import { API_URL } from '../api';

interface NavbarStore {
  slug: string;
  name: string;
  logoUrl: string | null;
}

function BagIcon() {
  return (
    <svg viewBox="0 0 24 24" className="h-5 w-5" fill="none" stroke="currentColor" strokeWidth="1.8" aria-hidden="true">
      <path
        strokeLinecap="round"
        strokeLinejoin="round"
        d="M6.5 8h11l1 11.5a1.5 1.5 0 0 1-1.5 1.6H7a1.5 1.5 0 0 1-1.5-1.6L6.5 8zm2.5 0V6.5a3 3 0 0 1 6 0V8"
      />
    </svg>
  );
}

export default function Navbar({ store, children }: { store?: NavbarStore; children?: ReactNode }) {
  const { t } = useTranslation();
  const logo = resolveAssetUrl(API_URL, store?.logoUrl);
  // Null outside a store's pages (the directory landing page has no cart).
  const cart = useOptionalCart();

  return (
    <header className="sticky top-0 z-30 border-b border-slate-200 bg-white/80 backdrop-blur">
      <div className="mx-auto flex h-14 max-w-6xl items-center justify-between px-4">
        {store ? (
          <Link to={`/${store.slug}`} className="flex items-center gap-2 text-lg font-bold text-slate-900">
            <span className="inline-flex h-8 w-8 items-center justify-center overflow-hidden rounded-lg bg-emerald-600 text-white">
              {logo ? (
                <img src={logo} alt={store.name} className="h-full w-full object-cover" />
              ) : (
                store.name.charAt(0)
              )}
            </span>
            <span className="line-clamp-1">{store.name}</span>
          </Link>
        ) : (
          <span aria-hidden />
        )}
        <div className="flex items-center gap-3">
          {children}
          {store && cart && (
            <Link
              to={`/${store.slug}/cart`}
              aria-label={t('cart.title')}
              className="relative inline-flex h-9 w-9 items-center justify-center rounded-xl text-slate-600 transition hover:bg-slate-100 hover:text-emerald-600"
            >
              <BagIcon />
              {cart.itemCount > 0 && (
                <span className="absolute -right-0.5 -top-0.5 inline-flex h-5 min-w-5 items-center justify-center rounded-full bg-emerald-600 px-1 text-xs font-semibold text-white">
                  {cart.itemCount}
                </span>
              )}
            </Link>
          )}
          <LanguageSwitcher />
        </div>
      </div>
    </header>
  );
}
