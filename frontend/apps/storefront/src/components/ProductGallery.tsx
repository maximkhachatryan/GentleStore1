import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { PublicImage } from '@gentlestore/shared';
import { resolveImageUrl } from '@gentlestore/shared';
import { API_URL } from '../api';

interface Props {
  images: PublicImage[];
  alt: string;
}

export default function ProductGallery({ images, alt }: Props) {
  const { t } = useTranslation();
  const [index, setIndex] = useState(0);

  if (images.length === 0) {
    return (
      <div className="flex aspect-square items-center justify-center rounded-2xl bg-slate-100 text-slate-300">
        {t('gallery.noImage')}
      </div>
    );
  }

  const current = resolveImageUrl(API_URL, images[index]?.imageUrl, 'lg');

  return (
    <div>
      <div className="flex max-h-[70vh] items-center justify-center overflow-hidden rounded-2xl bg-slate-100">
        <img src={current} alt={alt} className="max-h-[70vh] w-auto max-w-full object-contain" />
      </div>
      {images.length > 1 && (
        <div className="mt-3 flex gap-2 overflow-x-auto pb-1">
          {images.map((img, i) => (
            <button
              key={i}
              type="button"
              onClick={() => setIndex(i)}
              className={`h-16 w-16 flex-shrink-0 overflow-hidden rounded-lg border-2 ${
                i === index ? 'border-emerald-500' : 'border-transparent'
              }`}
            >
              <img src={resolveImageUrl(API_URL, img.imageUrl, 'sm')} alt="" className="h-full w-full object-cover" />
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
