import { Link } from 'react-router-dom';
import type { ReactNode } from 'react';
import { resolveAssetUrl } from '@gentlestore/shared';
import LanguageSwitcher from './LanguageSwitcher';
import { API_URL } from '../api';

interface NavbarStore {
  slug: string;
  name: string;
  logoUrl: string | null;
}

export default function Navbar({ store, children }: { store?: NavbarStore; children?: ReactNode }) {
  const logo = resolveAssetUrl(API_URL, store?.logoUrl);
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
          <LanguageSwitcher />
        </div>
      </div>
    </header>
  );
}
