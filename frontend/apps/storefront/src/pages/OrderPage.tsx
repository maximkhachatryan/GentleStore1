import { Link, useParams, useSearchParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import type { PublicOrder } from '@gentlestore/shared';
import Navbar from '../components/Navbar';
import StoreGate from '../components/StoreGate';
import ContactButtons from '../components/ContactButtons';
import OrderStatusBadge from '../components/OrderStatusBadge';
import { formatPrice } from '../lib/format';
import { isInviteRequired } from '../lib/access';
import { api } from '../api';

export default function OrderPage() {
  const { t, i18n } = useTranslation();
  const { slug = '', id = '' } = useParams();
  const [params] = useSearchParams();
  // Set by the checkout redirect so the "thank you" framing only shows the first time.
  const justPlaced = params.get('placed') === '1';

  const storeQuery = useQuery({ queryKey: ['store', slug], queryFn: () => api.store.get(slug), retry: false });
  const orderQuery = useQuery({
    queryKey: ['store', slug, 'order', id],
    queryFn: () => api.store.order(slug, id),
    retry: false,
  });

  if (isInviteRequired(storeQuery.error) || isInviteRequired(orderQuery.error))
    return <StoreGate slug={slug} reason="locked" />;

  const store = storeQuery.data;
  const order = orderQuery.data;

  if (orderQuery.isError) {
    return (
      <div className="flex min-h-screen flex-col bg-slate-50">
        <Navbar store={store} />
        <main className="mx-auto max-w-md px-4 py-24 text-center">
          <h1 className="text-xl font-bold text-slate-900">{t('order.notFound')}</h1>
          <p className="mt-2 text-sm text-slate-500">{t('order.notFoundHint')}</p>
          <Link to={`/${slug}`} className="mt-6 inline-block font-medium text-emerald-600">
            ← {t('product.backToStore')}
          </Link>
        </main>
      </div>
    );
  }

  return (
    <div className="flex min-h-screen flex-col bg-slate-50">
      <Navbar store={store} />

      <main className="mx-auto w-full max-w-2xl flex-1 space-y-4 px-4 py-6">
        {order ? (
          <>
            <div className="rounded-2xl border border-slate-200 bg-white p-5">
              {justPlaced && (
                <p className="mb-2 text-sm font-semibold text-emerald-600">{t('order.placed')}</p>
              )}
              <div className="flex flex-wrap items-center justify-between gap-2">
                <h1 className="text-2xl font-bold text-slate-900">{order.orderNumber}</h1>
                <OrderStatusBadge status={order.status} />
              </div>
              <p className="mt-1 text-sm text-slate-500">
                {new Date(order.placedAt).toLocaleString(i18n.language, {
                  dateStyle: 'medium',
                  timeStyle: 'short',
                })}
              </p>

              {/* The handoff that makes a web order real for a WhatsApp-first shop: the customer
                  sends the number to the store, and the store confirms from its own inbox. */}
              {store && (
                <div className="mt-4 rounded-xl bg-emerald-50 p-3">
                  <p className="text-sm font-semibold text-emerald-900">{t('order.confirmTitle')}</p>
                  <p className="mt-1 text-xs leading-relaxed text-emerald-800">
                    {t('order.confirmBody', { store: store.name })}
                  </p>
                  <div className="mt-3">
                    <ContactButtons
                      size="lg"
                      phone={store.phone}
                      message={whatsappMessage(order, store.name, t)}
                    />
                  </div>
                </div>
              )}
            </div>

            <section className="rounded-2xl border border-slate-200 bg-white">
              <div className="divide-y divide-slate-100">
                {order.lines.map((line, index) => (
                  <div key={index} className="flex items-start justify-between gap-3 p-4">
                    <div className="min-w-0">
                      <div className="font-medium text-slate-900">{line.productName}</div>
                      {line.variantLabel && <div className="text-xs text-slate-500">{line.variantLabel}</div>}
                      <div className="mt-0.5 text-xs text-slate-500">
                        {t('order.quantity', { count: line.quantity })}
                        {line.unitPrice !== null && ` · ${formatPrice(line.unitPrice, order.currency)}`}
                      </div>
                    </div>
                    <div className="whitespace-nowrap text-sm font-semibold text-slate-900">
                      {line.lineTotal === null ? t('cart.priceOnRequest') : formatPrice(line.lineTotal, order.currency)}
                    </div>
                  </div>
                ))}
              </div>
              <div className="flex items-baseline justify-between border-t border-slate-200 p-4">
                <span className="font-semibold text-slate-900">{t('cart.total')}</span>
                <span className="text-lg font-bold text-emerald-700">
                  {order.total === null ? t('cart.quoteNeeded') : formatPrice(order.total, order.currency)}
                </span>
              </div>
            </section>

            {order.awaitingQuote && (
              <p className="rounded-2xl bg-amber-50 px-4 py-3 text-sm text-amber-800">
                {t('order.awaitingQuote')}
              </p>
            )}

            <section className="space-y-2 rounded-2xl border border-slate-200 bg-white p-4 text-sm">
              <Detail label={t('checkout.fulfilment')} value={t(`checkout.${order.fulfilment.toLowerCase()}`)} />
              {order.deliveryAddress && <Detail label={t('checkout.address')} value={order.deliveryAddress} />}
              <Detail label={t('checkout.name')} value={order.contactName} />
              <Detail label={t('checkout.phone')} value={order.contactPhoneMasked} />
              {order.note && <Detail label={t('checkout.note')} value={order.note} />}
            </section>

            <div className="flex flex-wrap gap-3 pb-4">
              <Link
                to={`/${slug}`}
                className="rounded-xl border border-slate-300 bg-white px-4 py-2.5 text-sm font-semibold text-slate-700 transition hover:bg-slate-50"
              >
                {t('order.keepShopping')}
              </Link>
              <Link
                to={`/${slug}/orders`}
                className="rounded-xl border border-slate-300 bg-white px-4 py-2.5 text-sm font-semibold text-slate-700 transition hover:bg-slate-50"
              >
                {t('orders.title')}
              </Link>
            </div>
          </>
        ) : (
          <div className="h-64 animate-pulse rounded-2xl bg-slate-100" />
        )}
      </main>
    </div>
  );
}

function Detail({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-baseline justify-between gap-4">
      <span className="text-slate-500">{label}</span>
      <span className="text-right font-medium text-slate-900">{value}</span>
    </div>
  );
}

/** Summary the store can read at a glance in its own WhatsApp inbox. */
function whatsappMessage(order: PublicOrder, storeName: string, t: TFunction): string {
  const items = order.lines
    .map((l) => `• ${l.quantity} × ${l.productName}${l.variantLabel ? ` (${l.variantLabel})` : ''}`)
    .join('\n');

  return t('order.whatsappMessage', {
    store: storeName,
    number: order.orderNumber,
    items,
    total: order.total === null ? t('cart.quoteNeeded') : formatPrice(order.total, order.currency),
  });
}
