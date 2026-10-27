import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { BrowserRouter } from 'react-router-dom';
import App from './App';
import NotFoundPage from './pages/NotFoundPage';
import { api, ON_STORE_DOMAIN } from './api';
import './i18n';
import './index.css';

const queryClient = new QueryClient({
  defaultOptions: { queries: { retry: 1, refetchOnWindowFocus: false, staleTime: 30_000 } },
});

/** On a store's own domain, which store that is; undefined on the platform's domain. */
async function findHostStore(): Promise<string | null | undefined> {
  if (!ON_STORE_DOMAIN) return undefined;
  try {
    return (await api.host(window.location.hostname)).slug;
  } catch {
    return null;
  }
}

findHostStore().then((hostSlug) => {
  createRoot(document.getElementById('root')!).render(
    <StrictMode>
      <QueryClientProvider client={queryClient}>
        <BrowserRouter>
          {/* A domain pointed at us that no store claims (or one a store just gave up). */}
          {hostSlug === null ? <NotFoundPage /> : <App hostSlug={hostSlug} />}
        </BrowserRouter>
      </QueryClientProvider>
    </StrictMode>,
  );
});
