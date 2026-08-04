import { useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import Navbar from '../components/Navbar';
import Footer from '../components/Footer';
import Chip from '../components/Chip';
import ProductCard from '../components/ProductCard';
import ContactButtons from '../components/ContactButtons';
import { GridSkeleton } from '../components/Skeletons';
import { resolveAssetUrl } from '@gentlestore/shared';
import { api, API_URL } from '../api';

export default function StorePage() {
  const { slug = '' } = useParams();
  const [categoryId, setCategoryId] = useState<string | undefined>();
  const [tagId, setTagId] = useState<string | undefined>();
  const [search, setSearch] = useState('');

  const storeQuery = useQuery({ queryKey: ['store', slug], queryFn: () => api.store.get(slug), retry: false });
  const categoriesQuery = useQuery({
    queryKey: ['store', slug, 'categories'],
    queryFn: () => api.store.categories(slug),
    enabled: storeQuery.isSuccess,
  });
  const tagsQuery = useQuery({
    queryKey: ['store', slug, 'tags'],
    queryFn: () => api.store.tags(slug),
    enabled: storeQuery.isSuccess,
  });
  const productsQuery = useQuery({
    queryKey: ['store', slug, 'products', categoryId ?? 'all', tagId ?? 'all', search],
    queryFn: () => api.store.products(slug, { categoryId, tagId, search: search || undefined }),
    enabled: storeQuery.isSuccess,
  });

  if (storeQuery.isError) {
    return (
      <div>
        <Navbar />
        <div className="mx-auto max-w-md px-4 py-24 text-center">
          <h1 className="text-2xl font-bold text-slate-900">Store not found</h1>
          <p className="mt-2 text-slate-500">This store may be unavailable.</p>
          <Link to="/" className="mt-4 inline-block font-medium text-emerald-600">
            ← Back to all stores
          </Link>
        </div>
      </div>
    );
  }

  const store = storeQuery.data;
  const logo = resolveAssetUrl(API_URL, store?.logoUrl);

  return (
    <div className="flex min-h-screen flex-col">
      <Navbar />

      <section className="border-b border-slate-200 bg-white">
        <div className="mx-auto max-w-6xl px-4 py-8">
          {store ? (
            <div className="flex flex-col gap-5 sm:flex-row sm:items-center">
              <div className="flex h-20 w-20 items-center justify-center overflow-hidden rounded-2xl border border-slate-100 bg-slate-50 shadow">
                {logo ? (
                  <img src={logo} alt={store.name} className="h-full w-full object-cover" />
                ) : (
                  <span className="text-3xl font-bold text-emerald-600">{store.name.charAt(0)}</span>
                )}
              </div>
              <div className="flex-1">
                <h1 className="text-2xl font-bold text-slate-900">{store.name}</h1>
                {store.description && <p className="mt-1 max-w-2xl text-slate-500">{store.description}</p>}
              </div>
              <div className="sm:w-64">
                <ContactButtons phone={store.phone} message={`Hi ${store.name}, I have a question about your products.`} />
              </div>
            </div>
          ) : (
            <div className="h-20 animate-pulse rounded-2xl bg-slate-100" />
          )}
        </div>
      </section>

      <div className="sticky top-14 z-20 border-b border-slate-200 bg-slate-50/90 backdrop-blur">
        <div className="mx-auto max-w-6xl space-y-3 px-4 py-3">
          <input
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Search products..."
            className="w-full rounded-xl border border-slate-200 px-4 py-2 outline-none focus:border-emerald-400"
          />
          <div className="flex gap-2 overflow-x-auto pb-1">
            <Chip active={!categoryId} onClick={() => setCategoryId(undefined)}>
              All
            </Chip>
            {categoriesQuery.data?.map((c) => (
              <Chip key={c.id} active={categoryId === c.id} onClick={() => setCategoryId(c.id)}>
                {c.name}
              </Chip>
            ))}
          </div>
          {tagsQuery.data && tagsQuery.data.length > 0 && (
            <div className="flex items-center gap-2 overflow-x-auto pb-1">
              <span className="mr-1 self-center text-xs text-slate-400">Tags:</span>
              {tagsQuery.data.map((t) => (
                <button
                  key={t.id}
                  type="button"
                  onClick={() => setTagId(tagId === t.id ? undefined : t.id)}
                  className={`whitespace-nowrap rounded-full border px-3 py-1 text-xs font-medium ${
                    tagId === t.id ? 'border-slate-900 bg-slate-900 text-white' : 'border-slate-200 bg-white text-slate-600'
                  }`}
                >
                  {t.name}
                </button>
              ))}
            </div>
          )}
        </div>
      </div>

      <main className="mx-auto w-full max-w-6xl flex-1 px-4 py-8">
        {productsQuery.isLoading ? (
          <GridSkeleton />
        ) : productsQuery.data && productsQuery.data.length > 0 ? (
          <div className="grid grid-cols-2 gap-4 md:grid-cols-3 lg:grid-cols-4">
            {productsQuery.data.map((p) => (
              <ProductCard key={p.id} slug={slug} product={p} />
            ))}
          </div>
        ) : (
          <div className="py-16 text-center text-slate-500">No products found.</div>
        )}
      </main>

      <Footer />
    </div>
  );
}
