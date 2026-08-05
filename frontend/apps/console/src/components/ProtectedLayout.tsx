import { useMemo } from 'react';
import { Link, Navigate, Outlet, useLocation, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Avatar, Button, Layout, Menu, Typography } from 'antd';
import {
  AppstoreOutlined,
  DashboardOutlined,
  LogoutOutlined,
  ProfileOutlined,
  ShopOutlined,
  TagsOutlined,
  TeamOutlined,
  BranchesOutlined,
} from '@ant-design/icons';
import { useAuth } from '../auth/AuthContext';
import LanguageSwitcher from './LanguageSwitcher';

const { Header, Sider, Content } = Layout;

export default function ProtectedLayout() {
  const { t } = useTranslation();
  const { user, logout } = useAuth();
  const location = useLocation();
  const navigate = useNavigate();

  const isAdmin = user?.role === 'SuperAdmin';

  const items = useMemo(() => {
    if (isAdmin) {
      return [
        { key: '/admin', icon: <DashboardOutlined />, label: <Link to="/admin">{t('nav.dashboard')}</Link> },
        { key: '/admin/stores', icon: <ShopOutlined />, label: <Link to="/admin/stores">{t('nav.stores')}</Link> },
        { key: '/admin/users', icon: <TeamOutlined />, label: <Link to="/admin/users">{t('nav.users')}</Link> },
      ];
    }
    return [
      { key: '/store', icon: <ProfileOutlined />, label: <Link to="/store">{t('nav.storeProfile')}</Link> },
      { key: '/store/categories', icon: <AppstoreOutlined />, label: <Link to="/store/categories">{t('nav.categories')}</Link> },
      { key: '/store/products', icon: <ShopOutlined />, label: <Link to="/store/products">{t('nav.products')}</Link> },
      { key: '/store/tags', icon: <TagsOutlined />, label: <Link to="/store/tags">{t('nav.tags')}</Link> },
      { key: '/store/variant-attributes', icon: <BranchesOutlined />, label: <Link to="/store/variant-attributes">{t('nav.variantAttributes')}</Link> },
    ];
  }, [isAdmin, t]);

  const selectedKey = useMemo(() => {
    const matches = items
      .map((i) => i.key)
      .filter((k) => location.pathname === k || location.pathname.startsWith(`${k}/`))
      .sort((a, b) => b.length - a.length);
    return matches[0] ?? items[0]?.key;
  }, [items, location.pathname]);

  if (!user) {
    return <Navigate to="/login" replace state={{ from: location }} />;
  }

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  return (
    <Layout style={{ minHeight: '100vh' }}>
      <Sider theme="light" breakpoint="lg" collapsedWidth="0" style={{ borderRight: '1px solid #f0f0f0' }}>
        <div style={{ padding: 20, fontWeight: 700, fontSize: 18, color: '#4f46e5' }}>GentleStore</div>
        <Menu mode="inline" selectedKeys={[selectedKey]} items={items} style={{ borderInlineEnd: 'none' }} />
      </Sider>
      <Layout>
        <Header
          style={{
            background: '#fff',
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'center',
            paddingInline: 24,
            borderBottom: '1px solid #f0f0f0',
          }}
        >
          <Typography.Text type="secondary">{isAdmin ? t('layout.adminPanel') : user.storeName}</Typography.Text>
          <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
            <LanguageSwitcher />
            <Avatar style={{ backgroundColor: '#4f46e5' }}>{user.fullName.charAt(0).toUpperCase()}</Avatar>
            <span>{user.fullName}</span>
            <Button icon={<LogoutOutlined />} onClick={handleLogout}>
              {t('layout.logout')}
            </Button>
          </div>
        </Header>
        <Content style={{ margin: 24 }}>
          <Outlet />
        </Content>
      </Layout>
    </Layout>
  );
}
