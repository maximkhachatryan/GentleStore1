import { useTranslation } from 'react-i18next';
import type { OrderStatus } from '@gentlestore/shared';

const STYLES: Record<OrderStatus, string> = {
  New: 'bg-slate-100 text-slate-700',
  AwaitingQuote: 'bg-amber-100 text-amber-800',
  Quoted: 'bg-sky-100 text-sky-800',
  Confirmed: 'bg-emerald-100 text-emerald-800',
  Ready: 'bg-emerald-600 text-white',
  Completed: 'bg-slate-800 text-white',
  Cancelled: 'bg-rose-100 text-rose-700',
};

export default function OrderStatusBadge({ status }: { status: OrderStatus }) {
  const { t } = useTranslation();
  return (
    <span className={`inline-flex rounded-full px-2.5 py-1 text-xs font-semibold ${STYLES[status]}`}>
      {t(`orderStatus.${status}`)}
    </span>
  );
}
