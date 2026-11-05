import { useTranslation } from 'react-i18next';
import type { StoreTheme } from '@gentlestore/shared';
import { websiteLabel } from '../lib/website';

export default function Footer({ store }: { store?: { name: string; theme?: StoreTheme | null } }) {
  const { t } = useTranslation();
  const year = new Date().getFullYear();
  const website = store?.theme?.websiteUrl;

  return (
    <footer className="mt-16 border-t border-slate-200 py-8 text-center text-sm text-slate-500">
      <p>{store ? `© ${year} ${store.name}` : t('footer.text')}</p>
      {website && (
        <p className="mt-2">
          <a href={website} className="font-medium text-brand-700 transition hover:text-brand-600">
            {websiteLabel(website)}
          </a>
        </p>
      )}
    </footer>
  );
}
