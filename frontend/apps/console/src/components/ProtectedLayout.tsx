import { useEffect, useMemo, useState } from 'react';
import { Link, Navigate, Outlet, useLocation, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Avatar, Button, Drawer, Dropdown, Layout, Menu, Space, Tag, Typography } from 'antd';
import type { MenuProps } from 'antd';
import {
  AppstoreOutlined,
  BranchesOutlined,
  CheckOutlined,
  DashboardOutlined,
  DownOutlined,
  ExportOutlined,
  GlobalOutlined,
  LoginOutlined,
  LogoutOutlined,
  MenuOutlined,
  ProfileOutlined,
  ShopOutlined,
  ShoppingCartOutlined,
  TagsOutlined,
  TeamOutlined,
  UserOutlined,
} from '@ant-design/icons';
import { useAuth } from '../auth/AuthContext';
import { managedStore, useManagedStore } from '../auth/managedStore';
import { STOREFRONT_URL } from '../api';
import { useResponsive } from '../hooks/useResponsive';
import { SUPPORTED_LANGUAGES, LANGUAGE_NAMES, type Language } from '../i18n';
import { BRAND } from '../theme';
import Brand from './Brand';

const { Header, Sider, Content } = Layout;

const BORDER = '1px solid #eceef3';

export default function ProtectedLayout() {
  const { t, i18n } = useTranslation();
  const { user, logout } = useAuth();
  const location = useLocation();
  const navigate = useNavigate();
  const { isMobile, isDesktop } = useResponsive();
  const [navOpen, setNavOpen] = useState(false);

  const isAdmin = user?.role === 'SuperAdmin';
  const managed = useManagedStore();
  const managingStore = isAdmin && managed !== null;

  const items = useMemo(() => {
    const backofficeItems = [
      { key: '/store', icon: <ProfileOutlined />, label: <Link to="/store">{t('nav.storeProfile')}</Link> },
      { key: '/store/orders', icon: <ShoppingCartOutlined />, label: <Link to="/store/orders">{t('nav.orders')}</Link> },
      { key: '/store/categories', icon: <AppstoreOutlined />, label: <Link to="/store/categories">{t('nav.categories')}</Link> },
      { key: '/store/products', icon: <ShopOutlined />, label: <Link to="/store/products">{t('nav.products')}</Link> },
      { key: '/store/tags', icon: <TagsOutlined />, label: <Link to="/store/tags">{t('nav.tags')}</Link> },
      { key: '/store/variant-attributes', icon: <BranchesOutlined />, label: <Link to="/store/variant-attributes">{t('nav.variantAttributes')}</Link> },
      { key: '/store/customers', icon: <UserOutlined />, label: <Link to="/store/customers">{t('nav.customers')}</Link> },
    ];
    if (managingStore) return backofficeItems;
    if (isAdmin) {
      return [
        { key: '/admin', icon: <DashboardOutlined />, label: <Link to="/admin">{t('nav.dashboard')}</Link> },
        { key: '/admin/stores', icon: <ShopOutlined />, label: <Link to="/admin/stores">{t('nav.stores')}</Link> },
        { key: '/admin/users', icon: <TeamOutlined />, label: <Link to="/admin/users">{t('nav.users')}</Link> },
      ];
    }
    return backofficeItems;
  }, [isAdmin, managingStore, t]);

  const selectedKey = useMemo(() => {
    const matches = items
      .map((i) => i.key)
      .filter((k) => location.pathname === k || location.pathname.startsWith(`${k}/`))
      .sort((a, b) => b.length - a.length);
    return matches[0] ?? items[0]?.key;
  }, [items, location.pathname]);

  // Navigating (or growing past the mobile breakpoint) should never leave the nav sheet open.
  useEffect(() => setNavOpen(false), [location.pathname]);
  useEffect(() => {
    if (isDesktop) setNavOpen(false);
  }, [isDesktop]);

  if (!user) {
    return <Navigate to="/login" replace state={{ from: location }} />;
  }

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  const exitManageMode = () => {
    managedStore.clear();
    navigate('/admin/stores');
  };

  const storefrontSlug = managingStore ? managed?.slug : !isAdmin ? user.storeSlug : null;
  const storefrontUrl = storefrontSlug ? `${STOREFRONT_URL}/${storefrontSlug}` : STOREFRONT_URL;
  const contextLabel = managingStore ? managed?.name : isAdmin ? t('layout.adminPanel') : user.storeName;

  const navMenu = (
    <Menu
      mode="inline"
      selectedKeys={[selectedKey]}
      items={items}
      style={{ borderInlineEnd: 'none', paddingBlock: 8 }}
      onClick={() => setNavOpen(false)}
    />
  );

  // Everything renders inline — nested flyout submenus are unusable on a phone.
  const accountItems: MenuProps['items'] = [
    ...(isDesktop
      ? []
      : [
          {
            key: 'context',
            type: 'group' as const,
            label: user.fullName,
            children: [
              {
                key: 'storefront',
                icon: <ExportOutlined />,
                label: (
                  <a href={storefrontUrl} target="_blank" rel="noreferrer">
                    {t('layout.viewStorefront')}
                  </a>
                ),
              },
              ...(managingStore
                ? [{ key: 'exit-manage', icon: <LoginOutlined />, label: t('layout.exitManage') }]
                : []),
            ],
          },
          { type: 'divider' as const },
        ]),
    {
      key: 'language',
      type: 'group',
      label: (
        <Space size={6}>
          <GlobalOutlined />
          {t('layout.language')}
        </Space>
      ),
      children: SUPPORTED_LANGUAGES.map((lng) => ({
        key: `lang:${lng}`,
        label: LANGUAGE_NAMES[lng as Language],
        icon: i18n.language === lng ? <CheckOutlined /> : <span style={{ display: 'inline-block', width: 14 }} />,
      })),
    },
    { type: 'divider' },
    { key: 'logout', icon: <LogoutOutlined />, label: t('layout.logout'), danger: true },
  ];

  const onAccountClick: MenuProps['onClick'] = ({ key }) => {
    if (key === 'logout') handleLogout();
    else if (key === 'exit-manage') exitManageMode();
    else if (key.startsWith('lang:')) i18n.changeLanguage(key.slice('lang:'.length));
  };

  return (
    <Layout style={{ minHeight: '100dvh' }}>
      {isDesktop && (
        <Sider
          theme="light"
          width={240}
          style={{
            position: 'sticky',
            insetBlockStart: 0,
            height: '100dvh',
            overflow: 'auto',
            borderInlineEnd: BORDER,
          }}
        >
          <div style={{ padding: '18px 20px' }}>
            <Brand />
          </div>
          {navMenu}
        </Sider>
      )}

      <Layout style={{ minWidth: 0 }}>
        <Header
          style={{
            position: 'sticky',
            insetBlockStart: 0,
            zIndex: 20,
            display: 'flex',
            alignItems: 'center',
            gap: 8,
            paddingInline: isMobile ? 12 : 24,
            background: '#fff',
            borderBottom: BORDER,
            lineHeight: 'normal',
          }}
        >
          {!isDesktop && (
            <Button
              type="text"
              icon={<MenuOutlined />}
              aria-label={t('layout.menu')}
              onClick={() => setNavOpen(true)}
            />
          )}

          <div style={{ display: 'flex', alignItems: 'center', gap: 8, minWidth: 0, overflow: 'hidden' }}>
            <Typography.Text type="secondary" ellipsis style={{ fontSize: 14 }}>
              {contextLabel}
            </Typography.Text>
            {managingStore && !isMobile && (
              <Tag color="blue" style={{ marginInlineEnd: 0, flexShrink: 0 }}>
                {t('layout.managingStore')}
              </Tag>
            )}
          </div>

          <div style={{ marginInlineStart: 'auto', display: 'flex', alignItems: 'center', gap: 8 }}>
            {isDesktop && (
              <>
                <Button icon={<ExportOutlined />} href={storefrontUrl} target="_blank" rel="noreferrer">
                  {t('layout.viewStorefront')}
                </Button>
                {managingStore && (
                  <Button icon={<LoginOutlined />} onClick={exitManageMode}>
                    {t('layout.exitManage')}
                  </Button>
                )}
              </>
            )}
            <Dropdown
              trigger={['click']}
              placement="bottomRight"
              menu={{ items: accountItems, onClick: onAccountClick }}
            >
              <Button
                type="text"
                aria-label={t('layout.account')}
                style={{ height: 44, paddingInline: 6, display: 'flex', alignItems: 'center', gap: 8 }}
              >
                <Avatar size={30} style={{ backgroundColor: BRAND, flexShrink: 0 }}>
                  {user.fullName.charAt(0).toUpperCase()}
                </Avatar>
                {isDesktop && (
                  <>
                    <span style={{ maxWidth: 160, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                      {user.fullName}
                    </span>
                    <DownOutlined style={{ fontSize: 10, opacity: 0.5 }} />
                  </>
                )}
              </Button>
            </Dropdown>
          </div>
        </Header>

        <Content style={{ padding: isMobile ? '16px 12px 32px' : '24px 24px 40px' }}>
          <div style={{ width: '100%', maxWidth: 1180, marginInline: 'auto' }}>
            <Outlet />
          </div>
        </Content>
      </Layout>

      <Drawer
        open={navOpen}
        onClose={() => setNavOpen(false)}
        placement="left"
        size={272}
        title={<Brand />}
        styles={{ body: { padding: 0 } }}
      >
        <div style={{ padding: '14px 20px', borderBottom: BORDER }}>
          <Typography.Text type="secondary" style={{ fontSize: 12, textTransform: 'uppercase', letterSpacing: 0.4 }}>
            {managingStore ? t('layout.managingStore') : t('layout.workspace')}
          </Typography.Text>
          <div style={{ fontWeight: 600, marginTop: 2, wordBreak: 'break-word' }}>{contextLabel}</div>
        </div>
        {navMenu}
        {managingStore && (
          <div style={{ padding: '4px 20px 20px' }}>
            <Button block icon={<LoginOutlined />} onClick={exitManageMode}>
              {t('layout.exitManage')}
            </Button>
          </div>
        )}
      </Drawer>
    </Layout>
  );
}
