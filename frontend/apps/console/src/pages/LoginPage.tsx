import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { App as AntApp, Button, Card, Form, Input, Typography } from 'antd';
import { LockOutlined, MailOutlined } from '@ant-design/icons';
import { useAuth } from '../auth/AuthContext';
import LanguageSwitcher from '../components/LanguageSwitcher';

interface LoginValues {
  email: string;
  password: string;
}

export default function LoginPage() {
  const { t } = useTranslation();
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
      message.error(t('login.invalid'));
    } finally {
      setLoading(false);
    }
  };

  return (
    <div
      style={{
        minHeight: '100dvh',
        display: 'grid',
        placeItems: 'center',
        background: 'linear-gradient(135deg,#eef2ff,#faf5ff)',
        padding: '24px 16px calc(24px + env(safe-area-inset-bottom))',
      }}
    >
      <Card style={{ width: '100%', maxWidth: 390, boxShadow: '0 10px 40px rgba(0,0,0,0.08)' }}>
        <div style={{ display: 'flex', justifyContent: 'flex-end', marginBottom: 8 }}>
          <LanguageSwitcher />
        </div>
        <div style={{ textAlign: 'center', marginBottom: 24 }}>
          <Typography.Title level={3} style={{ marginBottom: 0, color: '#4f46e5' }}>
            GentleStore
          </Typography.Title>
          <Typography.Text type="secondary">{t('login.title')}</Typography.Text>
        </div>
        <Form<LoginValues> layout="vertical" onFinish={onFinish} requiredMark={false}>
          <Form.Item name="email" label={t('login.email')} rules={[{ required: true, message: t('login.emailRequired') }]}>
            <Input
              size="large"
              prefix={<MailOutlined />}
              placeholder="admin@gentlestore.local"
              autoComplete="username"
              inputMode="email"
            />
          </Form.Item>
          <Form.Item name="password" label={t('login.password')} rules={[{ required: true, message: t('login.passwordRequired') }]}>
            <Input.Password
              size="large"
              prefix={<LockOutlined />}
              placeholder="Your password"
              autoComplete="current-password"
            />
          </Form.Item>
          <Button type="primary" htmlType="submit" size="large" block loading={loading}>
            {t('login.signIn')}
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
