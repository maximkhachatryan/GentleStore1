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

export default function App() {
  return (
    <Routes>
      <Route path="/" element={<HomePage />} />
      <Route path="/:slug" element={<StoreLayout />}>
        <Route index element={<StorePage />} />
        {/* Target of the personal invite links; the secret arrives in the URL fragment. */}
        <Route path="welcome" element={<WelcomePage />} />
        <Route path="product/:id" element={<ProductPage />} />
        <Route path="cart" element={<CartPage />} />
        <Route path="orders" element={<OrdersPage />} />
        <Route path="orders/:id" element={<OrderPage />} />
      </Route>
      <Route path="*" element={<NotFoundPage />} />
    </Routes>
  );
}
