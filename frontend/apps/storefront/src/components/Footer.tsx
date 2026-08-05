import { useTranslation } from 'react-i18next';

export default function Footer({ store }: { store?: { name: string } }) {
  const { t } = useTranslation();
  const year = new Date().getFullYear();
  return (
    <footer className="mt-16 border-t border-slate-200 py-8 text-center text-sm text-slate-500">
      <p>{store ? `© ${year} ${store.name}` : t('footer.text')}</p>
    </footer>
  );
}
