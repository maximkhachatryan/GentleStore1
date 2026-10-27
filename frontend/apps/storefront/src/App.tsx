import { Route, Routes } from 'react-router-dom';
import StoreLayout from './components/StoreLayout';
import HomePage from './pages/HomePage';
import StorePage from './pages/StorePage';
import ProductPage from './pages/ProductPage';
import WelcomePage from './pages/WelcomePage';
import CartPage from './pages/CartPage';
import OrderPage from './pages/OrderPage';
import OrdersPage from './pages/OrdersPage';
import NotFoundPage from './pages/NotFoundPage';

// A store's pages, mounted under /:slug on the platform's domain and at / on the store's own.
// Keep in sync with the links the API builds (backend Storefront/StorefrontLinks.cs).
const storeRoutes = (
  <>
    <Route index element={<StorePage />} />
    <Route path="category/:categoryId" element={<StorePage />} />
    {/* Target of the personal invite links; the secret arrives in the URL fragment. */}
    <Route path="welcome" element={<WelcomePage />} />
    <Route path="product/:id" element={<ProductPage />} />
    <Route path="cart" element={<CartPage />} />
    <Route path="orders" element={<OrdersPage />} />
    <Route path="orders/:id" element={<OrderPage />} />
  </>
);

/** @param hostSlug The store whose own domain the app was opened on; absent on the platform's domain. */
export default function App({ hostSlug }: { hostSlug?: string }) {
  return (
    <Routes>
      {hostSlug ? (
        <Route path="/" element={<StoreLayout hostSlug={hostSlug} />}>
          {storeRoutes}
        </Route>
      ) : (
        <>
          <Route path="/" element={<HomePage />} />
          <Route path="/:slug" element={<StoreLayout />}>
            {storeRoutes}
          </Route>
        </>
      )}
      <Route path="*" element={<NotFoundPage />} />
    </Routes>
  );
}
