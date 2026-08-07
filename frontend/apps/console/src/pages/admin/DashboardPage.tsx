import type { ReactNode } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Card, Col, Row, Skeleton, Statistic } from 'antd';
import { AppstoreOutlined, CheckCircleOutlined, ShopOutlined, TeamOutlined } from '@ant-design/icons';
import { api } from '../../api';
import PageHeader from '../../components/PageHeader';

interface StatCardProps {
  title: string;
  value?: number;
  icon: ReactNode;
  tint: string;
}

function StatCard({ title, value, icon, tint }: StatCardProps) {
  return (
    <Card
      size="small"
      style={{ height: '100%' }}
      styles={{ body: { padding: 14, display: 'flex', alignItems: 'center', gap: 12 } }}
    >
      <div
        style={{
          width: 40,
          height: 40,
          flexShrink: 0,
          borderRadius: 12,
          display: 'grid',
          placeItems: 'center',
          fontSize: 18,
          background: tint,
        }}
      >
        {icon}
      </div>
      <div style={{ minWidth: 0 }}>
        <Statistic title={title} value={value ?? 0} />
      </div>
    </Card>
  );
}

export default function DashboardPage() {
  const { t } = useTranslation();
  const { data, isLoading } = useQuery({ queryKey: ['admin', 'stats'], queryFn: () => api.admin.stats() });

  const cards: StatCardProps[] = [
    { title: t('dashboard.stores'), value: data?.storeCount, icon: <ShopOutlined style={{ color: '#4f46e5' }} />, tint: '#eef2ff' },
    { title: t('dashboard.activeStores'), value: data?.activeStoreCount, icon: <CheckCircleOutlined style={{ color: '#059669' }} />, tint: '#ecfdf5' },
    { title: t('dashboard.products'), value: data?.productCount, icon: <AppstoreOutlined style={{ color: '#d97706' }} />, tint: '#fffbeb' },
    { title: t('dashboard.users'), value: data?.userCount, icon: <TeamOutlined style={{ color: '#0891b2' }} />, tint: '#ecfeff' },
  ];

  return (
    <div>
      <PageHeader title={t('dashboard.title')} />
      {isLoading ? (
        <Skeleton active />
      ) : (
        <Row gutter={[12, 12]}>
          {cards.map((card) => (
            <Col key={card.title} xs={12} xl={6}>
              <StatCard {...card} />
            </Col>
          ))}
        </Row>
      )}
    </div>
  );
}
