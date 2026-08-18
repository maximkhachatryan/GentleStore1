import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import type { PublicOrder, PublicStore } from '@gentlestore/shared';
import { api } from '../api';

interface Props {
  slug: string;
  order: PublicOrder;
  store?: PublicStore;
}

/**
 * The paying half of an order page.
 *
 * A payment is started server-side and the customer is sent wherever the provider says — usually a
 * hosted page on the provider's own domain, so no card details ever touch this app.
 */
export default function PaymentPanel({ slug, order, store }: Props) {
  const { t, i18n } = useTranslation();
  const queryClient = useQueryClient();
  const [errorKey, setErrorKey] = useState<string | null>(null);

  const methodsQuery = useQuery({
    queryKey: ['store', slug, 'payment-methods'],
    queryFn: () => api.store.paymentMethods(slug),
    enabled: order.awaitingPayment,
    staleTime: 5 * 60 * 1000,
  });

  const pay = useMutation({
    mutationFn: (method?: string) => api.store.startPayment(slug, order.id, { method: method ?? null }),
    onSuccess: (result) => {
      setErrorKey(null);

      if (result.action?.kind === 'Redirect' && result.action.url) {
        // Leaving the app entirely: assign rather than push, so the browser's back button returns
        // to the order rather than into a half-finished payment.
        window.location.assign(result.action.url);
        return;
      }

      // No action left means it settled server-side, or was already paid.
      queryClient.invalidateQueries({ queryKey: ['store', slug, 'order', order.id] });
    },
    onError: (error: unknown) => {
      const code = (error as { response?: { data?: { code?: string } } })?.response?.data?.code;
      setErrorKey(code ? `order.payError_${code}` : 'order.payError_payment_unavailable');
    },
  });

  if (order.paidAt) {
    return (
      <section className="rounded-2xl border border-emerald-200 bg-emerald-50 p-4">
        <p className="text-sm font-semibold text-emerald-900">{t('order.paid')}</p>
        <p className="mt-1 text-xs text-emerald-800">
          {t('order.paidOn', {
            date: new Date(order.paidAt).toLocaleString(i18n.language, { dateStyle: 'medium', timeStyle: 'short' }),
          })}
        </p>
      </section>
    );
  }

  if (!order.awaitingPayment) {
    // Priced but not yet payable — the store still has to quote it. The order page already says so.
    return null;
  }

  if (order.paymentStatus === 'Processing') {
    return (
      <section className="rounded-2xl border border-sky-200 bg-sky-50 p-4">
        <p className="text-sm text-sky-900">{t('order.paymentProcessing')}</p>
      </section>
    );
  }

  const methods = methodsQuery.data ?? [];
  const failed = order.paymentStatus === 'Failed';
  const message = errorKey ? t(errorKey) : null;

  return (
    <section className="rounded-2xl border border-slate-200 bg-white p-5">
      <h2 className="text-base font-bold text-slate-900">{t('order.payTitle')}</h2>
      {store && <p className="mt-1 text-sm text-slate-500">{t('order.payBody', { store: store.name })}</p>}

      {failed && <p className="mt-3 rounded-xl bg-rose-50 px-3 py-2 text-sm text-rose-800">{t('order.paymentFailed')}</p>}
      {message && <p className="mt-3 rounded-xl bg-rose-50 px-3 py-2 text-sm text-rose-800">{message}</p>}

      {methodsQuery.isLoading ? (
        <div className="mt-4 h-11 animate-pulse rounded-xl bg-slate-100" />
      ) : methods.length === 0 ? (
        <p className="mt-3 rounded-xl bg-amber-50 px-3 py-2 text-sm text-amber-800">{t('order.noMethods')}</p>
      ) : (
        <div className="mt-4 flex flex-col gap-2">
          {methods.map((method) => (
            <button
              key={method.key}
              type="button"
              disabled={pay.isPending}
              onClick={() => pay.mutate(method.key)}
              className="rounded-xl bg-emerald-600 px-4 py-3 text-sm font-semibold text-white transition hover:bg-emerald-700 disabled:opacity-60"
            >
              {pay.isPending
                ? t('order.checkingPayment')
                : methods.length === 1
                  ? failed
                    ? t('order.payRetry')
                    : t('order.payNow')
                  : t('order.payWith', { method: method.displayName })}
            </button>
          ))}
        </div>
      )}
    </section>
  );
}
