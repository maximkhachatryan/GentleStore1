import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { App as AntApp, Button, Card, Form, Input, Typography } from 'antd';
import { LockOutlined, MailOutlined } from '@ant-design/icons';
import { useAuth } from '../auth/AuthContext';

interface LoginValues {
  email: string;
  password: string;
}

export default function LoginPage() {
  const { login } = useAuth();
  const navigate = useNavigate();
  const { message } = AntApp.useApp();
  const [loading, setLoading] = useState(false);

  const onFinish = async (values: LoginValues) => {
    setLoading(true);
    try {
      const user = await login(values.email, values.password);
      navigate(user.role === 'SuperAdmin' ? '/admin' : '/store', { replace: true });
    } catch {
      message.error('Invalid email or password.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div
      style={{
        minHeight: '100vh',
        display: 'grid',
        placeItems: 'center',
        background: 'linear-gradient(135deg,#eef2ff,#faf5ff)',
        padding: 16,
      }}
    >
      <Card style={{ width: 380, boxShadow: '0 10px 40px rgba(0,0,0,0.08)' }}>
        <div style={{ textAlign: 'center', marginBottom: 24 }}>
          <Typography.Title level={3} style={{ marginBottom: 0, color: '#4f46e5' }}>
            GentleStore
          </Typography.Title>
          <Typography.Text type="secondary">Sign in to your console</Typography.Text>
        </div>
        <Form<LoginValues> layout="vertical" onFinish={onFinish} requiredMark={false}>
          <Form.Item name="email" label="Email" rules={[{ required: true, message: 'Email is required' }]}>
            <Input prefix={<MailOutlined />} placeholder="admin@gentlestore.local" autoComplete="username" />
          </Form.Item>
          <Form.Item name="password" label="Password" rules={[{ required: true, message: 'Password is required' }]}>
            <Input.Password prefix={<LockOutlined />} placeholder="Your password" autoComplete="current-password" />
          </Form.Item>
          <Button type="primary" htmlType="submit" block loading={loading}>
            Sign in
          </Button>
        </Form>
        <Typography.Paragraph type="secondary" style={{ marginTop: 16, fontSize: 12, textAlign: 'center' }}>
          Admin: admin@gentlestore.local / Admin123!
          <br />
          Store owner: owner@bloom-petal.local / Owner123!
        </Typography.Paragraph>
      </Card>
    </div>
  );
}
