import { useMemo } from 'react';
import { Link, Navigate, Outlet, useLocation, useNavigate } from 'react-router-dom';
import { Avatar, Button, Layout, Menu, Typography } from 'antd';
import {
  AppstoreOutlined,
  DashboardOutlined,
  LogoutOutlined,
  ProfileOutlined,
  ShopOutlined,
  TagsOutlined,
  TeamOutlined,
} from '@ant-design/icons';
import { useAuth } from '../auth/AuthContext';

const { Header, Sider, Content } = Layout;

export default function ProtectedLayout() {
  const { user, logout } = useAuth();
  const location = useLocation();
  const navigate = useNavigate();

  const isAdmin = user?.role === 'SuperAdmin';

  const items = useMemo(() => {
    if (isAdmin) {
      return [
        { key: '/admin', icon: <DashboardOutlined />, label: <Link to="/admin">Dashboard</Link> },
        { key: '/admin/stores', icon: <ShopOutlined />, label: <Link to="/admin/stores">Stores</Link> },
        { key: '/admin/users', icon: <TeamOutlined />, label: <Link to="/admin/users">Users</Link> },
      ];
    }
    return [
      { key: '/store', icon: <ProfileOutlined />, label: <Link to="/store">Store Profile</Link> },
      { key: '/store/categories', icon: <AppstoreOutlined />, label: <Link to="/store/categories">Categories</Link> },
      { key: '/store/products', icon: <ShopOutlined />, label: <Link to="/store/products">Products</Link> },
      { key: '/store/tags', icon: <TagsOutlined />, label: <Link to="/store/tags">Tags</Link> },
    ];
  }, [isAdmin]);

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
          <Typography.Text type="secondary">{isAdmin ? 'Admin Panel' : user.storeName}</Typography.Text>
          <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
            <Avatar style={{ backgroundColor: '#4f46e5' }}>{user.fullName.charAt(0).toUpperCase()}</Avatar>
            <span>{user.fullName}</span>
            <Button icon={<LogoutOutlined />} onClick={handleLogout}>
              Logout
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
