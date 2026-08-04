import { useState } from 'react';
import type { PublicImage } from '@gentlestore/shared';
import { resolveAssetUrl } from '@gentlestore/shared';
import { API_URL } from '../api';

interface Props {
  images: PublicImage[];
  alt: string;
}

export default function ProductGallery({ images, alt }: Props) {
  const [index, setIndex] = useState(0);

  if (images.length === 0) {
    return (
      <div className="flex aspect-square items-center justify-center rounded-2xl bg-slate-100 text-slate-300">
        No image
      </div>
    );
  }

  const current = resolveAssetUrl(API_URL, images[index]?.imageUrl);

  return (
    <div>
      <div className="aspect-square overflow-hidden rounded-2xl bg-slate-100">
        <img src={current} alt={alt} className="h-full w-full object-cover" />
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
              <img src={resolveAssetUrl(API_URL, img.imageUrl)} alt="" className="h-full w-full object-cover" />
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
