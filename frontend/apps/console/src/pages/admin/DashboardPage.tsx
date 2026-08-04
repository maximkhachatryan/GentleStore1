import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Card, Col, Row, Spin, Statistic, Typography } from 'antd';
import { AppstoreOutlined, CheckCircleOutlined, ShopOutlined, TeamOutlined } from '@ant-design/icons';
import { api } from '../../api';

export default function DashboardPage() {
  const { t } = useTranslation();
  const { data, isLoading } = useQuery({ queryKey: ['admin', 'stats'], queryFn: () => api.admin.stats() });

  return (
    <div>
      <Typography.Title level={3}>{t('dashboard.title')}</Typography.Title>
      {isLoading ? (
        <Spin />
      ) : (
        <Row gutter={[16, 16]}>
          <Col xs={12} md={6}>
            <Card>
              <Statistic title={t('dashboard.stores')} value={data?.storeCount} prefix={<ShopOutlined />} />
            </Card>
          </Col>
          <Col xs={12} md={6}>
            <Card>
              <Statistic title={t('dashboard.activeStores')} value={data?.activeStoreCount} prefix={<CheckCircleOutlined />} />
            </Card>
          </Col>
          <Col xs={12} md={6}>
            <Card>
              <Statistic title={t('dashboard.products')} value={data?.productCount} prefix={<AppstoreOutlined />} />
            </Card>
          </Col>
          <Col xs={12} md={6}>
            <Card>
              <Statistic title={t('dashboard.users')} value={data?.userCount} prefix={<TeamOutlined />} />
            </Card>
          </Col>
        </Row>
      )}
    </div>
  );
}
