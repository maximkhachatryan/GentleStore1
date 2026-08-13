import { Navigate, Route, Routes } from 'react-router-dom';
import { Spin } from 'antd';
import { useAuth } from './auth/AuthContext';
import { useManagedStore } from './auth/managedStore';
import ProtectedLayout from './components/ProtectedLayout';
import LoginPage from './pages/LoginPage';
import DashboardPage from './pages/admin/DashboardPage';
import StoresPage from './pages/admin/StoresPage';
import UsersPage from './pages/admin/UsersPage';
import StoreProfilePage from './pages/backoffice/StoreProfilePage';
import CategoriesPage from './pages/backoffice/CategoriesPage';
import ProductsPage from './pages/backoffice/ProductsPage';
import TagsPage from './pages/backoffice/TagsPage';
import VariantAttributesPage from './pages/backoffice/VariantAttributesPage';
import CustomersPage from './pages/backoffice/CustomersPage';

export default function App() {
  const { user, loading } = useAuth();
  const managedStore = useManagedStore();

  if (loading) {
    return (
      <div style={{ minHeight: '100vh', display: 'grid', placeItems: 'center' }}>
        <Spin size="large" />
      </div>
    );
  }

  const isAdmin = user?.role === 'SuperAdmin';
  const managingStore = isAdmin && managedStore !== null;
  const showBackoffice = !isAdmin || managingStore;

  return (
    <Routes>
      <Route path="/login" element={user ? <Navigate to="/" replace /> : <LoginPage />} />
      <Route element={<ProtectedLayout />}>
        {isAdmin && (
          <>
            <Route path="/admin" element={<DashboardPage />} />
            <Route path="/admin/stores" element={<StoresPage />} />
            <Route path="/admin/users" element={<UsersPage />} />
          </>
        )}
        {showBackoffice && (
          <>
            <Route path="/store" element={<StoreProfilePage />} />
            <Route path="/store/categories" element={<CategoriesPage />} />
            <Route path="/store/products" element={<ProductsPage />} />
            <Route path="/store/tags" element={<TagsPage />} />
            <Route path="/store/variant-attributes" element={<VariantAttributesPage />} />
            <Route path="/store/customers" element={<CustomersPage />} />
          </>
        )}
        <Route path="*" element={<Navigate to={isAdmin ? (managingStore ? '/store' : '/admin') : '/store'} replace />} />
      </Route>
    </Routes>
  );
}
