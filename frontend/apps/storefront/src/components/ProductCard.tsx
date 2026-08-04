import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import type { PublicProductListItem } from '@gentlestore/shared';
import { resolveAssetUrl } from '@gentlestore/shared';
import { API_URL } from '../api';
import { formatPrice } from '../lib/format';

interface Props {
  slug: string;
  product: PublicProductListItem;
}

export default function ProductCard({ slug, product }: Props) {
  const { t } = useTranslation();
  const img = resolveAssetUrl(API_URL, product.primaryImageUrl);

  return (
    <Link
      to={`/${slug}/product/${product.id}`}
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
      </div>
      <div className="p-3">
        <h3 className="line-clamp-1 font-medium text-slate-900">{product.name}</h3>
        <div className="mt-1 font-semibold text-emerald-700">{formatPrice(product.price, product.currency)}</div>
        {product.tags.length > 0 && (
          <div className="mt-2 flex flex-wrap gap-1">
            {product.tags.slice(0, 3).map((t) => (
              <span key={t} className="rounded-full bg-emerald-50 px-2 py-0.5 text-[11px] font-medium text-emerald-700">
                {t}
              </span>
            ))}
          </div>
        )}
      </div>
    </Link>
  );
}
