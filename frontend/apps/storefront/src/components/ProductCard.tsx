import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import type { PublicProductListItem } from '@gentlestore/shared';
import { resolveImageUrl } from '@gentlestore/shared';
import { API_URL } from '../api';
import { discountPercent, formatPrice } from '../lib/format';
import { useStorePath } from '../lib/storeBase';

interface Props {
  product: PublicProductListItem;
}

export default function ProductCard({ product }: Props) {
  const { t } = useTranslation();
  const storePath = useStorePath();
  const img = resolveImageUrl(API_URL, product.primaryImageUrl, 'md');
  const percentOff = discountPercent(product.price, product.regularPrice);

  return (
    <Link
      to={storePath(`/product/${product.id}`)}
      className="group block overflow-hidden rounded-2xl border border-slate-200 bg-white transition hover:shadow-lg"
    >
      <div className="relative aspect-square overflow-hidden bg-slate-100">
        {img ? (
          <img
            src={img}
            alt={product.name}
            loading="lazy"
            className="h-full w-full object-cover transition duration-300 group-hover:scale-105"
          />
        ) : (
          <div className="flex h-full items-center justify-center text-slate-300">{t('productCard.noImage')}</div>
        )}
        {!product.inStock && (
          <span className="absolute left-2 top-2 rounded-full bg-slate-900/80 px-2.5 py-1 text-xs font-medium text-white">
            {t('productCard.outOfStock')}
          </span>
        )}
        {percentOff !== null && (
          <span className="absolute right-2 top-2 rounded-full bg-rose-600 px-2.5 py-1 text-xs font-semibold text-white">
            −{percentOff}%
          </span>
        )}
      </div>
      <div className="p-3">
        <h3 className="line-clamp-1 font-medium text-slate-900">{product.name}</h3>
        <div className="mt-1 flex flex-wrap items-baseline gap-x-2">
          <span className="font-semibold text-brand-700">
            {product.price === null ? t('productCard.priceOnRequest') : formatPrice(product.price, product.currency)}
          </span>
          {percentOff !== null && (
            <span className="text-sm text-slate-400 line-through">
              {formatPrice(product.regularPrice!, product.currency)}
            </span>
          )}
        </div>
        {product.tags.length > 0 && (
          <div className="mt-2 flex flex-wrap gap-1">
            {product.tags.slice(0, 3).map((t) => (
              <span key={t} className="rounded-full bg-brand-50 px-2 py-0.5 text-[11px] font-medium text-brand-700">
                {t}
              </span>
            ))}
          </div>
        )}
      </div>
    </Link>
  );
}
