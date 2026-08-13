import { useEffect, useState } from 'react';
import { Navigate, useParams } from 'react-router-dom';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { resolveAssetUrl } from '@gentlestore/shared';
import Navbar from '../components/Navbar';
import StoreGate from '../components/StoreGate';
import { readInviteToken, redeemFailureReason, scrubInviteToken } from '../lib/access';
import { api, API_URL } from '../api';

/**
 * Landing page for a personal invite link. It claims the link for this browser — the first
 * browser to open it wins, and every later attempt from anywhere else is refused — then drops
 * the customer straight into the catalogue.
 */
export default function WelcomePage() {
  const { t } = useTranslation();
  const { slug = '' } = useParams();
  const queryClient = useQueryClient();

  // Read once, before the effect below wipes it from the address bar.
  const [token] = useState(readInviteToken);

  useEffect(() => {
    if (token) scrubInviteToken();
  }, [token]);

  // Fetched alongside the redemption so the wait looks like the store the customer expects
  // rather than an anonymous spinner.
  const accessQuery = useQuery({
    queryKey: ['store', slug, 'access'],
    queryFn: () => api.store.access(slug),
    retry: false,
  });

  // A query rather than a mutation on purpose: React Query de-duplicates in-flight queries, so a
  // double-mounted effect (StrictMode, a fast remount) cannot fire two competing claims — and
  // two competing claims would make the second one look like a stolen link.
  const redeemQuery = useQuery({
    queryKey: ['invite-redeem', slug, token],
    queryFn: () => api.store.redeemInvite(slug, token!),
    enabled: token !== null,
    retry: false,
    staleTime: Infinity,
    gcTime: Infinity,
    refetchOnMount: false,
    refetchOnWindowFocus: false,
  });

  const unlocked =
    redeemQuery.data?.status === 'unlocked' || redeemQuery.data?.status === 'already_unlocked';

  useEffect(() => {
    // Anything cached while the store was still locked (a 403 from a previous visit) has to go.
    if (unlocked) queryClient.invalidateQueries({ queryKey: ['store', slug] });
  }, [unlocked, queryClient, slug]);

  if (accessQuery.isError) {
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

  if (unlocked) return <Navigate to={`/${slug}`} replace />;

  // No token in the link: either they are already in (bookmarked the welcome page, or the store
  // is public) or there is nothing here to unlock.
  if (token === null) {
    if (accessQuery.isLoading) return <Unlocking storeName={undefined} logoUrl={undefined} />;
    return accessQuery.data?.unlocked ? (
      <Navigate to={`/${slug}`} replace />
    ) : (
      <StoreGate slug={slug} reason="locked" />
    );
  }

  if (redeemQuery.isError) return <StoreGate slug={slug} reason={redeemFailureReason(redeemQuery.error)} />;

  return <Unlocking storeName={accessQuery.data?.name} logoUrl={accessQuery.data?.logoUrl} />;
}

function Unlocking({ storeName, logoUrl }: { storeName?: string; logoUrl?: string | null }) {
  const { t } = useTranslation();
  const logo = resolveAssetUrl(API_URL, logoUrl);

  return (
    <div className="flex min-h-screen flex-col bg-slate-50">
      <Navbar />
      <main className="mx-auto flex w-full max-w-md flex-1 flex-col items-center justify-center px-4 py-12 text-center">
        <div className="flex h-16 w-16 items-center justify-center overflow-hidden rounded-2xl border border-slate-100 bg-white shadow-sm">
          {logo ? (
            <img src={logo} alt={storeName ?? ''} className="h-full w-full object-cover" />
          ) : (
            <span className="text-2xl font-bold text-emerald-600">{storeName?.charAt(0) ?? ''}</span>
          )}
        </div>
        <h1 className="mt-5 text-xl font-bold text-slate-900">
          {storeName ? t('welcome.title', { store: storeName }) : t('welcome.titleFallback')}
        </h1>
        <p className="mt-2 text-sm text-slate-500">{t('welcome.subtitle')}</p>
        <div
          className="mt-6 h-6 w-6 animate-spin rounded-full border-2 border-slate-200 border-t-emerald-600"
          role="status"
          aria-label={t('welcome.subtitle')}
        />
      </main>
    </div>
  );
}
