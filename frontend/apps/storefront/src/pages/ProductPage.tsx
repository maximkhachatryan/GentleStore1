import { Link, useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import Navbar from '../components/Navbar';
import ProductGallery from '../components/ProductGallery';
import ContactButtons from '../components/ContactButtons';
import { formatPrice } from '../lib/format';
import { api } from '../api';

export default function ProductPage() {
  const { t } = useTranslation();
  const { slug = '', id = '' } = useParams();

  const storeQuery = useQuery({ queryKey: ['store', slug], queryFn: () => api.store.get(slug), retry: false });
  const productQuery = useQuery({
    queryKey: ['store', slug, 'product', id],
    queryFn: () => api.store.product(slug, id),
    retry: false,
  });

  if (productQuery.isError || storeQuery.isError) {
    return (
      <div>
        <Navbar />
        <div className="mx-auto max-w-md px-4 py-24 text-center">
          <h1 className="text-2xl font-bold text-slate-900">{t('product.notFound')}</h1>
          <Link to={`/${slug}`} className="mt-4 inline-block font-medium text-emerald-600">
            ← {t('product.backToStore')}
          </Link>
        </div>
      </div>
    );
  }

  const product = productQuery.data;
  const store = storeQuery.data;

  return (
    <div className="min-h-screen pb-24 md:pb-0">
      <Navbar />
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
                {formatPrice(product.price, product.currency)}
              </div>
              <div className="mt-1">
                {product.inStock ? (
                  <span className="text-sm font-medium text-emerald-600">{t('product.inStock')}</span>
                ) : (
                  <span className="text-sm font-medium text-slate-400">{t('product.unavailable')}</span>
                )}
              </div>
              {product.description && (
                <p className="mt-4 whitespace-pre-line leading-relaxed text-slate-600">{product.description}</p>
              )}
              {store && (
                <div className="mt-6 hidden md:block">
                  <ContactButtons size="lg" phone={store.phone} message={t('product.orderMessage', { store: store.name, product: product.name })} />
                  <p className="mt-2 text-xs text-slate-400">{t('product.ordersNote')}</p>
                </div>
              )}
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

      {store && product && (
        <div className="fixed inset-x-0 bottom-0 z-30 border-t border-slate-200 bg-white p-3 md:hidden">
          <ContactButtons size="lg" phone={store.phone} message={t('product.orderMessage', { store: store.name, product: product.name })} />
        </div>
      )}
    </div>
  );
}
