import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import Navbar from '../components/Navbar';

export default function NotFoundPage() {
  const { t } = useTranslation();
  return (
    <div>
      <Navbar />
      <div className="mx-auto max-w-md px-4 py-24 text-center">
        <h1 className="text-3xl font-bold text-slate-900">{t('notFound.title')}</h1>
        <p className="mt-2 text-slate-500">{t('notFound.subtitle')}</p>
        <Link to="/" className="mt-6 inline-block rounded-xl bg-emerald-600 px-5 py-2.5 font-semibold text-white">
          {t('notFound.backHome')}
        </Link>
      </div>
    </div>
  );
}
