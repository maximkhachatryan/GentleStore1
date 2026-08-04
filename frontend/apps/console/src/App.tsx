import { Navigate, Route, Routes } from 'react-router-dom';
import { Spin } from 'antd';
import { useAuth } from './auth/AuthContext';
import ProtectedLayout from './components/ProtectedLayout';
import LoginPage from './pages/LoginPage';
import DashboardPage from './pages/admin/DashboardPage';
import StoresPage from './pages/admin/StoresPage';
import UsersPage from './pages/admin/UsersPage';
import StoreProfilePage from './pages/backoffice/StoreProfilePage';
import CategoriesPage from './pages/backoffice/CategoriesPage';
import ProductsPage from './pages/backoffice/ProductsPage';
import TagsPage from './pages/backoffice/TagsPage';

export default function App() {
  const { user, loading } = useAuth();

  if (loading) {
    return (
      <div style={{ minHeight: '100vh', display: 'grid', placeItems: 'center' }}>
        <Spin size="large" />
      </div>
    );
  }

  const isAdmin = user?.role === 'SuperAdmin';

  return (
    <Routes>
      <Route path="/login" element={user ? <Navigate to="/" replace /> : <LoginPage />} />
      <Route element={<ProtectedLayout />}>
        {isAdmin ? (
          <>
            <Route path="/admin" element={<DashboardPage />} />
            <Route path="/admin/stores" element={<StoresPage />} />
            <Route path="/admin/users" element={<UsersPage />} />
          </>
        ) : (
          <>
            <Route path="/store" element={<StoreProfilePage />} />
            <Route path="/store/categories" element={<CategoriesPage />} />
            <Route path="/store/products" element={<ProductsPage />} />
            <Route path="/store/tags" element={<TagsPage />} />
          </>
        )}
        <Route path="*" element={<Navigate to={isAdmin ? '/admin' : '/store'} replace />} />
      </Route>
    </Routes>
  );
}
