import { useTranslation } from 'react-i18next';

export default function Footer() {
  const { t } = useTranslation();
  return (
    <footer className="mt-16 border-t border-slate-200 py-8 text-center text-sm text-slate-500">
      <p>{t('footer.text')}</p>
    </footer>
  );
}
