import { useMemo, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import Navbar from '../components/Navbar';
import ProductGallery from '../components/ProductGallery';
import ContactButtons from '../components/ContactButtons';
import StoreGate from '../components/StoreGate';
import { formatPrice } from '../lib/format';
import { isInviteRequired } from '../lib/access';
import { useCart } from '../lib/cart';
import { api } from '../api';

export default function ProductPage() {
  const { t } = useTranslation();
  const { slug = '', id = '' } = useParams();
  const navigate = useNavigate();
  const cart = useCart();

  const storeQuery = useQuery({ queryKey: ['store', slug], queryFn: () => api.store.get(slug), retry: false });
  const productQuery = useQuery({
    queryKey: ['store', slug, 'product', id],
    queryFn: () => api.store.product(slug, id),
    retry: false,
  });

  const product = productQuery.data;
  const store = storeQuery.data;

  const attributeGroups = useMemo(() => {
    const groups = new Map<string, string[]>();
    for (const variant of product?.variants ?? []) {
      for (const attr of variant.attributes) {
        const values = groups.get(attr.name) ?? [];
        if (!values.includes(attr.value)) values.push(attr.value);
        groups.set(attr.name, values);
      }
    }
    return [...groups.entries()].map(([name, values]) => ({ name, values }));
  }, [product]);

  const [selected, setSelected] = useState<Record<string, string>>({});

  const matchedVariant = useMemo(() => {
    if (!product?.variants.length || attributeGroups.length === 0) return null;
    if (attributeGroups.some((g) => !selected[g.name])) return null;
    return (
      product.variants.find((v) =>
        attributeGroups.every((g) => v.attributes.some((a) => a.name === g.name && a.value === selected[g.name])),
      ) ?? null
    );
  }, [product, attributeGroups, selected]);

  const hasVariants = (product?.variants.length ?? 0) > 0;
  const minVariantPrice = hasVariants ? Math.min(...product!.variants.map((v) => v.price)) : null;
  const displayPrice = matchedVariant ? matchedVariant.price : hasVariants ? minVariantPrice : product?.price ?? null;
  const displayInStock = matchedVariant ? matchedVariant.inStock : hasVariants ? product!.variants.some((v) => v.inStock) : product?.inStock ?? false;

  const orderProductLabel = useMemo(() => {
    if (!product) return '';
    const parts = attributeGroups.map((g) => (selected[g.name] ? `${g.name}: ${selected[g.name]}` : null)).filter(Boolean);
    return parts.length ? `${product.name} (${parts.join(', ')})` : product.name;
  }, [product, attributeGroups, selected]);

  // With variants defined, nothing is orderable until one is fully specified — that is where the
  // price lives.
  const needsVariantChoice = hasVariants && !matchedVariant;
  const canAddToCart = product !== undefined && displayInStock && !needsVariantChoice;

  const addToCart = (thenCheckout: boolean) => {
    if (!product || !canAddToCart) return;

    cart.add({
      productId: product.id,
      variantId: matchedVariant?.id ?? null,
      name: product.name,
      variantLabel: matchedVariant
        ? attributeGroups.map((g) => `${g.name}: ${selected[g.name]}`).join(', ')
        : null,
      unitPrice: displayPrice,
      imageUrl: product.images[0]?.imageUrl ?? null,
    });

    if (thenCheckout) navigate(`/${slug}/cart`);
  };

  const addToCartControls = (
    <div className="space-y-2">
      <button
        type="button"
        disabled={!canAddToCart}
        onClick={() => addToCart(false)}
        className="w-full rounded-xl bg-emerald-600 px-5 py-3 text-base font-semibold text-white transition hover:bg-emerald-700 disabled:cursor-not-allowed disabled:bg-slate-200 disabled:text-slate-400"
      >
        {needsVariantChoice ? t('product.chooseOptions') : t('product.addToCart')}
      </button>
      {canAddToCart && (
        <button
          type="button"
          onClick={() => addToCart(true)}
          className="w-full rounded-xl border border-slate-300 px-5 py-2.5 text-sm font-semibold text-slate-700 transition hover:bg-slate-50"
        >
          {t('product.buyNow')}
        </button>
      )}
    </div>
  );

  if (productQuery.isError || storeQuery.isError) {
    if (isInviteRequired(storeQuery.error) || isInviteRequired(productQuery.error))
      return <StoreGate slug={slug} reason="locked" />;

    return (
      <div>
        <Navbar store={store} />
        <div className="mx-auto max-w-md px-4 py-24 text-center">
          <h1 className="text-2xl font-bold text-slate-900">{t('product.notFound')}</h1>
          <Link to={`/${slug}`} className="mt-4 inline-block font-medium text-emerald-600">
            ← {t('product.backToStore')}
          </Link>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen pb-24 md:pb-0">
      <Navbar store={store} />
      <div className="mx-auto max-w-5xl px-4 py-6">
        <Link to={`/${slug}`} className="text-sm text-slate-500 transition hover:text-emerald-600">
          ← {store?.name ?? t('product.backToStore')}
        </Link>

        {product ? (
          <div className="mt-4 grid gap-8 md:grid-cols-2">
            <ProductGallery images={product.images} alt={product.name} />
            <div>
              {product.tags.length > 0 && (
                <div className="flex flex-wrap gap-2">
                  {product.tags.map((t) => (
                    <span key={t.id} className="rounded-full bg-emerald-50 px-2.5 py-0.5 text-xs font-medium text-emerald-700">
                      {t.name}
                    </span>
                  ))}
                </div>
              )}
              <h1 className="mt-2 text-3xl font-bold text-slate-900">{product.name}</h1>
              <div className="mt-2 text-2xl font-semibold text-emerald-700">
                {displayPrice === null
                  ? t('product.priceOnRequest')
                  : hasVariants && !matchedVariant
                    ? t('product.priceFrom', { price: formatPrice(displayPrice, product.currency) })
                    : formatPrice(displayPrice, product.currency)}
              </div>
              <div className="mt-1">
                {displayInStock ? (
                  <span className="text-sm font-medium text-emerald-600">{t('product.inStock')}</span>
                ) : (
                  <span className="text-sm font-medium text-slate-400">{t('product.unavailable')}</span>
                )}
              </div>

              {attributeGroups.length > 0 && (
                <div className="mt-5 space-y-4">
                  {attributeGroups.map((group) => (
                    <div key={group.name}>
                      <div className="mb-1.5 text-sm font-medium text-slate-700">{group.name}</div>
                      <div className="flex flex-wrap gap-2">
                        {group.values.map((value) => {
                          const active = selected[group.name] === value;
                          return (
                            <button
                              key={value}
                              type="button"
                              onClick={() =>
                                setSelected((prev) =>
                                  prev[group.name] === value
                                    ? Object.fromEntries(Object.entries(prev).filter(([k]) => k !== group.name))
                                    : { ...prev, [group.name]: value },
                                )
                              }
                              className={`rounded-lg border px-3 py-1.5 text-sm font-medium transition ${
                                active
                                  ? 'border-emerald-600 bg-emerald-50 text-emerald-700'
                                  : 'border-slate-200 text-slate-700 hover:border-emerald-300'
                              }`}
                            >
                              {value}
                            </button>
                          );
                        })}
                      </div>
                    </div>
                  ))}
                </div>
              )}

              {product.description && (
                <p className="mt-4 whitespace-pre-line leading-relaxed text-slate-600">{product.description}</p>
              )}
              <div className="mt-6 hidden md:block">
                {addToCartControls}
                {store && (
                  <div className="mt-4 border-t border-slate-100 pt-4">
                    <p className="mb-2 text-xs text-slate-400">{t('product.orAskFirst')}</p>
                    <ContactButtons phone={store.phone} message={t('product.orderMessage', { store: store.name, product: orderProductLabel })} />
                  </div>
                )}
              </div>
            </div>
          </div>
        ) : (
          <div className="mt-4 grid gap-8 md:grid-cols-2">
            <div className="aspect-square animate-pulse rounded-2xl bg-slate-100" />
            <div className="space-y-3">
              <div className="h-8 animate-pulse rounded bg-slate-100" />
              <div className="h-6 w-1/3 animate-pulse rounded bg-slate-100" />
              <div className="h-24 animate-pulse rounded bg-slate-100" />
            </div>
          </div>
        )}
      </div>

      {product && (
        <div className="fixed inset-x-0 bottom-0 z-30 border-t border-slate-200 bg-white p-3 pb-[calc(0.75rem+env(safe-area-inset-bottom))] md:hidden">
          {addToCartControls}
        </div>
      )}
    </div>
  );
}
