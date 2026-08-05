import { useTranslation } from 'react-i18next';
import Navbar from '../components/Navbar';

export default function HomePage() {
  const { t } = useTranslation();

  return (
    <div className="flex min-h-screen flex-col">
      <Navbar />
      <main className="mx-auto flex w-full max-w-md flex-1 flex-col items-center justify-center px-4 py-24 text-center">
        <h1 className="text-2xl font-bold text-slate-900">{t('home.title')}</h1>
        <p className="mt-2 text-slate-500">{t('home.subtitle')}</p>
      </main>
    </div>
  );
}
