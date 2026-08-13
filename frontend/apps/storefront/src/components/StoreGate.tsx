import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { resolveAssetUrl } from '@gentlestore/shared';
import Navbar from './Navbar';
import ContactButtons from './ContactButtons';
import type { GateReason } from '../lib/access';
import { api, API_URL } from '../api';

interface Props {
  slug: string;
  reason: GateReason;
}

function LockIcon() {
  return (
    <svg viewBox="0 0 24 24" className="h-7 w-7" fill="none" stroke="currentColor" strokeWidth="1.8" aria-hidden="true">
      <path
        strokeLinecap="round"
        strokeLinejoin="round"
        d="M7 10.5V8a5 5 0 0 1 10 0v2.5M6 10.5h12a1.5 1.5 0 0 1 1.5 1.5v7A1.5 1.5 0 0 1 18 20.5H6A1.5 1.5 0 0 1 4.5 19v-7A1.5 1.5 0 0 1 6 10.5z"
      />
    </svg>
  );
}

/**
 * The screen a customer sees instead of the catalogue: either the store is invite-only and this
 * browser has no session, or the link they followed cannot be used. Every variant ends in the
 * same place — a prefilled WhatsApp message asking the store for a working link — because that
 * is the only action a locked-out customer can actually take.
 */
export default function StoreGate({ slug, reason }: Props) {
  const { t } = useTranslation();

  // Never gated: this is what makes the screen recognisable as *their* store rather than a
  // bare error page. It carries the store name, logo and the phone number to ask for help on.
  const { data: access, isLoading, isError } = useQuery({
    queryKey: ['store', slug, 'access'],
    queryFn: () => api.store.access(slug),
    retry: false,
  });

  if (isLoading) {
    return (
      <div className="flex min-h-screen flex-col">
        <Navbar />
        <div className="mx-auto w-full max-w-md flex-1 px-4 py-16">
          <div className="h-64 animate-pulse rounded-3xl bg-slate-100" />
        </div>
      </div>
    );
  }

  if (isError || !access) {
    return (
      <div className="flex min-h-screen flex-col">
        <Navbar />
        <div className="mx-auto max-w-md px-4 py-24 text-center">
          <h1 className="text-2xl font-bold text-slate-900">{t('store.notFound')}</h1>
          <p className="mt-2 text-slate-500">{t('store.unavailable')}</p>
        </div>
      </div>
    );
  }

  const logo = resolveAssetUrl(API_URL, access.logoUrl);
  // 'locked' and 'already_used' are the two cases where the browser itself is the reason, so
  // they are the ones worth explaining private windows and cleared cookies for.
  const explainBrowserBinding = reason === 'locked' || reason === 'already_used';

  return (
    <div className="flex min-h-screen flex-col bg-slate-50">
      <Navbar />

      <main className="mx-auto flex w-full max-w-md flex-1 flex-col justify-center px-4 py-12">
        <div className="rounded-3xl border border-slate-200 bg-white p-6 text-center shadow-sm sm:p-8">
          <div className="mx-auto flex h-16 w-16 items-center justify-center overflow-hidden rounded-2xl border border-slate-100 bg-slate-50">
            {logo ? (
              <img src={logo} alt={access.name} className="h-full w-full object-cover" />
            ) : (
              <span className="text-2xl font-bold text-emerald-600">{access.name.charAt(0)}</span>
            )}
          </div>

          <div className="mt-4 inline-flex items-center gap-1.5 rounded-full bg-slate-100 px-3 py-1 text-xs font-medium text-slate-500">
            <span className="text-emerald-600">
              <LockIcon />
            </span>
            {t('gate.privateBadge')}
          </div>

          <h1 className="mt-3 text-xl font-bold text-slate-900">{t(`gate.${reason}.title`)}</h1>
          <p className="mt-2 text-sm leading-relaxed text-slate-500">
            {t(`gate.${reason}.body`, { store: access.name })}
          </p>

          <div className="mt-6">
            <ContactButtons
              size="lg"
              phone={access.phone}
              message={t('gate.requestMessage', { store: access.name })}
            />
          </div>

          {explainBrowserBinding && (
            <p className="mt-4 text-xs leading-relaxed text-slate-400">{t('gate.browserNote')}</p>
          )}
        </div>

        <p className="mt-4 text-center text-xs text-slate-400">{t('gate.footerNote', { store: access.name })}</p>
      </main>
    </div>
  );
}
