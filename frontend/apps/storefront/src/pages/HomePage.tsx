import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import Navbar from '../components/Navbar';
import Footer from '../components/Footer';
import StoreCard from '../components/StoreCard';
import { CardSkeleton } from '../components/Skeletons';
import { api } from '../api';

export default function HomePage() {
  const [search, setSearch] = useState('');
  const { data: stores, isLoading } = useQuery({
    queryKey: ['stores', search],
    queryFn: () => api.store.list(search || undefined),
  });

  return (
    <div className="flex min-h-screen flex-col">
      <Navbar />

      <section className="bg-gradient-to-br from-emerald-600 to-teal-700 text-white">
        <div className="mx-auto max-w-6xl px-4 py-16 text-center">
          <h1 className="text-4xl font-bold tracking-tight md:text-5xl">Shop from local stores</h1>
          <p className="mt-3 text-lg text-emerald-50">
            Discover products and order directly via WhatsApp or a quick call.
          </p>
          <div className="mx-auto mt-8 max-w-md">
            <input
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Search stores..."
              className="w-full rounded-xl px-4 py-3 text-slate-900 shadow-lg outline-none focus:ring-2 focus:ring-white/60"
            />
          </div>
        </div>
      </section>

      <main className="mx-auto w-full max-w-6xl flex-1 px-4 py-10">
        <h2 className="mb-5 text-xl font-semibold text-slate-900">{search ? 'Search results' : 'Featured stores'}</h2>
        {isLoading ? (
          <div className="grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-3">
            {Array.from({ length: 6 }).map((_, i) => (
              <CardSkeleton key={i} />
            ))}
          </div>
        ) : stores && stores.length > 0 ? (
          <div className="grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-3">
            {stores.map((s) => (
              <StoreCard key={s.slug} store={s} />
            ))}
          </div>
        ) : (
          <p className="py-10 text-center text-slate-500">No stores found.</p>
        )}
      </main>

      <Footer />
    </div>
  );
}
