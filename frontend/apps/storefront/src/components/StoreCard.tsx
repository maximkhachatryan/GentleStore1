import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import type { PublicStoreListItem } from '@gentlestore/shared';
import { resolveAssetUrl } from '@gentlestore/shared';
import { API_URL } from '../api';

export default function StoreCard({ store }: { store: PublicStoreListItem }) {
  const { t } = useTranslation();
  const logo = resolveAssetUrl(API_URL, store.logoUrl);

  return (
    <Link
      to={`/${store.slug}`}
      className="group block overflow-hidden rounded-2xl border border-slate-200 bg-white transition hover:-translate-y-0.5 hover:shadow-lg"
    >
      <div className="h-28 bg-gradient-to-br from-emerald-500 to-teal-600" />
      <div className="-mt-10 p-4">
        <div className="flex h-16 w-16 items-center justify-center overflow-hidden rounded-xl border border-slate-100 bg-white shadow">
          {logo ? (
            <img src={logo} alt={store.name} className="h-full w-full object-cover" />
          ) : (
            <span className="text-2xl font-bold text-emerald-600">{store.name.charAt(0)}</span>
          )}
        </div>
        <h3 className="mt-3 font-semibold text-slate-900 group-hover:text-emerald-700">{store.name}</h3>
        {store.description && <p className="mt-1 line-clamp-2 text-sm text-slate-500">{store.description}</p>}
        <span className="mt-3 inline-block text-sm font-medium text-emerald-600">{t('storeCard.browse')} →</span>
      </div>
    </Link>
  );
}
