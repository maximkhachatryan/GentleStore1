import { Link, useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import Navbar from '../components/Navbar';
import StoreGate from '../components/StoreGate';
import OrderStatusBadge from '../components/OrderStatusBadge';
import { formatPrice } from '../lib/format';
import { isInviteRequired } from '../lib/access';
import { api } from '../api';

export default function OrdersPage() {
  const { t, i18n } = useTranslation();
  const { slug = '' } = useParams();

  const storeQuery = useQuery({ queryKey: ['store', slug], queryFn: () => api.store.get(slug), retry: false });
  const ordersQuery = useQuery({
    queryKey: ['store', slug, 'orders'],
    queryFn: () => api.store.orders(slug),
    enabled: storeQuery.isSuccess,
  });

  if (isInviteRequired(storeQuery.error)) return <StoreGate slug={slug} reason="locked" />;

  const store = storeQuery.data;
  const orders = ordersQuery.data ?? [];

  return (
    <div className="flex min-h-screen flex-col bg-slate-50">
      <Navbar store={store} />

      <main className="mx-auto w-full max-w-2xl flex-1 space-y-3 px-4 py-6">
        <h1 className="text-2xl font-bold text-slate-900">{t('orders.title')}</h1>

        {ordersQuery.isLoading ? (
          <div className="h-24 animate-pulse rounded-2xl bg-slate-100" />
        ) : orders.length === 0 ? (
          <div className="rounded-2xl border border-slate-200 bg-white px-4 py-12 text-center">
            <p className="font-semibold text-slate-900">{t('orders.empty')}</p>
            {/* Order history lives on the browser's session, so a cleared cookie loses the list. */}
            <p className="mt-1 text-sm text-slate-500">{t('orders.emptyHint')}</p>
            <Link
              to={`/${slug}`}
              className="mt-6 inline-block rounded-xl bg-emerald-600 px-5 py-2.5 font-semibold text-white transition hover:bg-emerald-700"
            >
              {t('cart.browse')}
            </Link>
          </div>
        ) : (
          orders.map((order) => (
            <Link
              key={order.id}
              to={`/${slug}/orders/${order.id}`}
              className="block rounded-2xl border border-slate-200 bg-white p-4 transition hover:border-emerald-300"
            >
              <div className="flex items-center justify-between gap-3">
                <span className="font-bold text-slate-900">{order.orderNumber}</span>
                <OrderStatusBadge status={order.status} />
              </div>
              <div className="mt-1 flex items-baseline justify-between gap-3 text-sm">
                <span className="text-slate-500">
                  {new Date(order.placedAt).toLocaleDateString(i18n.language, { dateStyle: 'medium' })}
                  {' · '}
                  {t('order.itemCount', { count: order.lines.reduce((sum, l) => sum + l.quantity, 0) })}
                </span>
                <span className="font-semibold text-emerald-700">
                  {order.total === null ? t('cart.quoteNeeded') : formatPrice(order.total, order.currency)}
                </span>
              </div>
            </Link>
          ))
        )}
      </main>
    </div>
  );
}
