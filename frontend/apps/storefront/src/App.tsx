import { Route, Routes } from 'react-router-dom';
import HomePage from './pages/HomePage';
import StorePage from './pages/StorePage';
import ProductPage from './pages/ProductPage';
import WelcomePage from './pages/WelcomePage';
import NotFoundPage from './pages/NotFoundPage';

export default function App() {
  return (
    <Routes>
      <Route path="/" element={<HomePage />} />
      {/* Target of the personal invite links; the secret arrives in the URL fragment. */}
      <Route path="/:slug/welcome" element={<WelcomePage />} />
      <Route path="/:slug" element={<StorePage />} />
      <Route path="/:slug/product/:id" element={<ProductPage />} />
      <Route path="*" element={<NotFoundPage />} />
    </Routes>
  );
}
