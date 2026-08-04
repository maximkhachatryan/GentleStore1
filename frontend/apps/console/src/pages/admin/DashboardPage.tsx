import { useQuery } from '@tanstack/react-query';
import { Card, Col, Row, Spin, Statistic, Typography } from 'antd';
import { AppstoreOutlined, CheckCircleOutlined, ShopOutlined, TeamOutlined } from '@ant-design/icons';
import { api } from '../../api';

export default function DashboardPage() {
  const { data, isLoading } = useQuery({ queryKey: ['admin', 'stats'], queryFn: () => api.admin.stats() });

  return (
    <div>
      <Typography.Title level={3}>Dashboard</Typography.Title>
      {isLoading ? (
        <Spin />
      ) : (
        <Row gutter={[16, 16]}>
          <Col xs={12} md={6}>
            <Card>
              <Statistic title="Stores" value={data?.storeCount} prefix={<ShopOutlined />} />
            </Card>
          </Col>
          <Col xs={12} md={6}>
            <Card>
              <Statistic title="Active stores" value={data?.activeStoreCount} prefix={<CheckCircleOutlined />} />
            </Card>
          </Col>
          <Col xs={12} md={6}>
            <Card>
              <Statistic title="Products" value={data?.productCount} prefix={<AppstoreOutlined />} />
            </Card>
          </Col>
          <Col xs={12} md={6}>
            <Card>
              <Statistic title="Users" value={data?.userCount} prefix={<TeamOutlined />} />
            </Card>
          </Col>
        </Row>
      )}
    </div>
  );
}
