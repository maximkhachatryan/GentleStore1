import { useEffect, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import axios from 'axios';
import { resolveAssetUrl } from '@gentlestore/shared';
import type { FulfilmentMethod } from '@gentlestore/shared';
import Navbar from '../components/Navbar';
import StoreGate from '../components/StoreGate';
import { formatPrice } from '../lib/format';
import { isInviteRequired } from '../lib/access';
import { useCart } from '../lib/cart';
import { api, API_URL } from '../api';

/** Maps the API's refusal codes onto copy; anything unrecognised falls back to a generic line. */
function checkoutErrorKey(error: unknown): string {
  if (!axios.isAxiosError(error)) return 'checkout.errors.generic';
  if (error.response?.status === 429) return 'checkout.errors.rate_limited';

  const code = (error.response?.data as { code?: string } | undefined)?.code;
  const known = [
    'cart_empty',
    'cart_too_large',
    'invalid_quantity',
    'address_required',
    'contact_required',
    'phone_missing_country_code',
    'phone_invalid',
    'product_unknown',
    'product_unavailable',
    'variant_required',
    'variant_unknown',
    'variant_unavailable',
    'customer_blocked',
  ];
  return code && known.includes(code) ? `checkout.errors.${code}` : 'checkout.errors.generic';
}

export default function CartPage() {
  const { t } = useTranslation();
  const { slug = '' } = useParams();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const cart = useCart();

  const storeQuery = useQuery({ queryKey: ['store', slug], queryFn: () => api.store.get(slug), retry: false });
  const store = storeQuery.data;

  const [fulfilment, setFulfilment] = useState<FulfilmentMethod>('Pickup');
  const [address, setAddress] = useState('');
  const [note, setNote] = useState('');
  const [name, setName] = useState('');
  const [phone, setPhone] = useState('');
  const [errorKey, setErrorKey] = useState<string | null>(null);

  // A browser the store already knows needs no contact form at all — that is the whole payoff of
  // having been invited.
  const known = store?.visitor ?? null;
  const knownName = known?.displayName ?? null;

  useEffect(() => {
    if (knownName) setName(knownName);
  }, [knownName]);

  const place = useMutation({
    mutationFn: () =>
      api.store.placeOrder(slug, {
        items: cart.lines.map((l) => ({
          productId: l.productId,
          variantId: l.variantId,
          quantity: l.quantity,
        })),
        fulfilment,
        deliveryAddress: fulfilment === 'Delivery' ? address : null,
        note: note || null,
        contactName: name || null,
        contactPhone: phone || null,
      }),
    onSuccess: (order) => {
      cart.clear();
      // The new order belongs in the history list, and a guest checkout just created a session.
      queryClient.invalidateQueries({ queryKey: ['store', slug] });
      navigate(`/${slug}/orders/${order.id}?placed=1`, { replace: true });
    },
    onError: (error) => setErrorKey(checkoutErrorKey(error)),
  });

  if (storeQuery.isError) {
    if (isInviteRequired(storeQuery.error)) return <StoreGate slug={slug} reason="locked" />;
    return (
      <div>
        <Navbar />
        <div className="mx-auto max-w-md px-4 py-24 text-center">
          <h1 className="text-2xl font-bold text-slate-900">{t('store.notFound')}</h1>
        </div>
      </div>
    );
  }

  const currency = store?.currency ?? 'USD';
  // Contact details are only asked for when the store has no record of this browser.
  const needsContact = known === null;
  const contactReady = !needsContact || (name.trim().length > 0 && phone.trim().length > 0);
  const addressReady = fulfilment === 'Pickup' || address.trim().length > 0;
  const canSubmit = cart.lines.length > 0 && contactReady && addressReady && !place.isPending;

  if (cart.lines.length === 0) {
    return (
      <div className="flex min-h-screen flex-col bg-slate-50">
        <Navbar store={store} />
        <main className="mx-auto flex w-full max-w-md flex-1 flex-col items-center justify-center px-4 py-20 text-center">
          <h1 className="text-xl font-bold text-slate-900">{t('cart.empty')}</h1>
          <p className="mt-2 text-sm text-slate-500">{t('cart.emptyHint')}</p>
          <Link
            to={`/${slug}`}
            className="mt-6 rounded-xl bg-emerald-600 px-5 py-2.5 font-semibold text-white transition hover:bg-emerald-700"
          >
            {t('cart.browse')}
          </Link>
        </main>
      </div>
    );
  }

  return (
    <div className="flex min-h-screen flex-col bg-slate-50">
      <Navbar store={store} />

      <main className="mx-auto w-full max-w-2xl flex-1 space-y-4 px-4 py-6">
        <h1 className="text-2xl font-bold text-slate-900">{t('cart.title')}</h1>

        <section className="divide-y divide-slate-100 rounded-2xl border border-slate-200 bg-white">
          {cart.lines.map((line) => {
            const image = resolveAssetUrl(API_URL, line.imageUrl);
            return (
              <div key={`${line.productId}:${line.variantId ?? ''}`} className="flex gap-3 p-3">
                <div className="h-16 w-16 flex-shrink-0 overflow-hidden rounded-xl bg-slate-100">
                  {image && <img src={image} alt={line.name} className="h-full w-full object-cover" />}
                </div>
                <div className="min-w-0 flex-1">
                  <div className="font-semibold text-slate-900">{line.name}</div>
                  {line.variantLabel && <div className="text-xs text-slate-500">{line.variantLabel}</div>}
                  <div className="mt-1 text-sm text-emerald-700">
                    {line.unitPrice === null
                      ? t('cart.priceOnRequest')
                      : formatPrice(line.unitPrice, currency)}
                  </div>

                  <div className="mt-2 flex items-center gap-2">
                    <div className="inline-flex items-center rounded-lg border border-slate-200">
                      <button
                        type="button"
                        aria-label={t('cart.decrease')}
                        onClick={() => cart.setQuantity(line.productId, line.variantId, line.quantity - 1)}
                        className="h-8 w-8 text-slate-600 transition hover:bg-slate-50"
                      >
                        −
                      </button>
                      <span className="w-8 text-center text-sm font-medium">{line.quantity}</span>
                      <button
                        type="button"
                        aria-label={t('cart.increase')}
                        onClick={() => cart.setQuantity(line.productId, line.variantId, line.quantity + 1)}
                        className="h-8 w-8 text-slate-600 transition hover:bg-slate-50"
                      >
                        +
                      </button>
                    </div>
                    <button
                      type="button"
                      onClick={() => cart.remove(line.productId, line.variantId)}
                      className="text-xs text-slate-400 underline transition hover:text-rose-600"
                    >
                      {t('cart.remove')}
                    </button>
                  </div>
                </div>
                <div className="text-right text-sm font-semibold text-slate-900">
                  {line.unitPrice === null ? '—' : formatPrice(line.unitPrice * line.quantity, currency)}
                </div>
              </div>
            );
          })}
        </section>

        <section className="space-y-4 rounded-2xl border border-slate-200 bg-white p-4">
          <div>
            <div className="mb-2 text-sm font-semibold text-slate-900">{t('checkout.fulfilment')}</div>
            <div className="grid grid-cols-2 gap-2">
              {(['Pickup', 'Delivery'] as const).map((option) => (
                <button
                  key={option}
                  type="button"
                  onClick={() => setFulfilment(option)}
                  className={`rounded-xl border px-4 py-2.5 text-sm font-medium transition ${
                    fulfilment === option
                      ? 'border-emerald-600 bg-emerald-50 text-emerald-700'
                      : 'border-slate-200 text-slate-600 hover:border-emerald-300'
                  }`}
                >
                  {t(`checkout.${option.toLowerCase()}`)}
                </button>
              ))}
            </div>
          </div>

          {fulfilment === 'Delivery' && (
            <label className="block">
              <span className="text-sm font-semibold text-slate-900">{t('checkout.address')}</span>
              <textarea
                value={address}
                onChange={(e) => setAddress(e.target.value)}
                rows={2}
                placeholder={t('checkout.addressPlaceholder')}
                className="mt-1 w-full rounded-xl border border-slate-200 px-3 py-2 outline-none focus:border-emerald-400"
              />
            </label>
          )}

          {needsContact ? (
            <>
              <label className="block">
                <span className="text-sm font-semibold text-slate-900">{t('checkout.name')}</span>
                <input
                  value={name}
                  onChange={(e) => setName(e.target.value)}
                  autoComplete="name"
                  className="mt-1 w-full rounded-xl border border-slate-200 px-3 py-2 outline-none focus:border-emerald-400"
                />
              </label>
              <label className="block">
                <span className="text-sm font-semibold text-slate-900">{t('checkout.phone')}</span>
                <input
                  value={phone}
                  onChange={(e) => setPhone(e.target.value)}
                  inputMode="tel"
                  autoComplete="tel"
                  placeholder="+374 99 12 34 56"
                  className="mt-1 w-full rounded-xl border border-slate-200 px-3 py-2 outline-none focus:border-emerald-400"
                />
                <span className="mt-1 block text-xs text-slate-400">{t('checkout.phoneHint')}</span>
              </label>
            </>
          ) : (
            <div className="rounded-xl bg-emerald-50 px-3 py-2.5 text-sm text-emerald-800">
              {knownName
                ? t('checkout.knownAs', { name: knownName })
                : t('checkout.knownAsPhone', { phone: known!.phoneMasked })}
            </div>
          )}

          <label className="block">
            <span className="text-sm font-semibold text-slate-900">{t('checkout.note')}</span>
            <textarea
              value={note}
              onChange={(e) => setNote(e.target.value)}
              rows={2}
              placeholder={t('checkout.notePlaceholder')}
              className="mt-1 w-full rounded-xl border border-slate-200 px-3 py-2 outline-none focus:border-emerald-400"
            />
          </label>
        </section>

        <section className="rounded-2xl border border-slate-200 bg-white p-4">
          <div className="flex items-baseline justify-between">
            <span className="font-semibold text-slate-900">{t('cart.total')}</span>
            <span className="text-xl font-bold text-emerald-700">
              {cart.subtotal === null ? t('cart.quoteNeeded') : formatPrice(cart.subtotal, currency)}
            </span>
          </div>
          {cart.subtotal === null && (
            <p className="mt-1 text-xs text-slate-500">{t('cart.quoteNeededHint')}</p>
          )}

          {errorKey && (
            <p className="mt-3 rounded-xl bg-rose-50 px-3 py-2 text-sm text-rose-700">{t(errorKey)}</p>
          )}

          <button
            type="button"
            disabled={!canSubmit}
            onClick={() => {
              setErrorKey(null);
              place.mutate();
            }}
            className="mt-4 w-full rounded-xl bg-emerald-600 px-5 py-3 text-base font-semibold text-white transition hover:bg-emerald-700 disabled:cursor-not-allowed disabled:bg-slate-200 disabled:text-slate-400"
          >
            {place.isPending ? t('checkout.placing') : t('checkout.placeOrder')}
          </button>
          <p className="mt-2 text-center text-xs text-slate-400">{t('checkout.confirmNote')}</p>
        </section>
      </main>
    </div>
  );
}
