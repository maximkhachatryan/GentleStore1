import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { App as AntApp, Button, Card, Col, Form, Input, Radio, Row, Space, Typography } from 'antd';
import type { StorefrontAccessMode } from '@gentlestore/shared';
import { api } from '../../api';
import ImageUpload from '../../components/ImageUpload';
import PageHeader from '../../components/PageHeader';
import { useResponsive } from '../../hooks/useResponsive';

interface ProfileValues {
  name: string;
  phone: string;
  currency: string;
  description?: string;
  storefrontAccess: StorefrontAccessMode;
}

/** One radio option, with the consequence of picking it spelled out under the label. */
function AccessOption({ value, title, description }: { value: StorefrontAccessMode; title: string; description: string }) {
  return (
    <Radio value={value} style={{ alignItems: 'flex-start', display: 'flex' }}>
      <div style={{ paddingBottom: 8 }}>
        <div style={{ fontWeight: 600 }}>{title}</div>
        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
          {description}
        </Typography.Text>
      </div>
    </Radio>
  );
}

export default function StoreProfilePage() {
  const { t } = useTranslation();
  const qc = useQueryClient();
  const { message } = AntApp.useApp();
  const { isMobile } = useResponsive();
  const [form] = Form.useForm<ProfileValues>();
  const [logoUrl, setLogoUrl] = useState<string | undefined>();

  const { data, isLoading } = useQuery({ queryKey: ['backoffice', 'store'], queryFn: () => api.backoffice.getStore() });

  useEffect(() => {
    if (data) {
      form.setFieldsValue({
        name: data.name,
        phone: data.phone,
        currency: data.currency,
        description: data.description ?? '',
        storefrontAccess: data.storefrontAccess,
      });
      setLogoUrl(data.logoUrl ?? undefined);
    }
  }, [data, form]);

  const save = useMutation({
    mutationFn: (v: ProfileValues) =>
      api.backoffice.updateStore({
        name: v.name,
        phone: v.phone,
        currency: v.currency || 'USD',
        description: v.description,
        logoUrl,
        storefrontAccess: v.storefrontAccess,
      }),
    onSuccess: () => {
      message.success(t('storeProfile.updated'));
      qc.invalidateQueries({ queryKey: ['backoffice', 'store'] });
    },
    onError: () => message.error(t('storeProfile.saveError')),
  });

  return (
    <div style={{ maxWidth: 640 }}>
      <PageHeader
        title={t('storeProfile.title')}
        subtitle={data ? `${t('storeProfile.publicUrl')}: /${data.slug}` : undefined}
      />
      <Card loading={isLoading}>
        <Form form={form} layout="vertical" onFinish={(v) => save.mutate(v)}>
          <Form.Item name="name" label={t('storeProfile.storeName')} rules={[{ required: true, message: t('common.nameRequired') }]}>
            <Input />
          </Form.Item>
          <Row gutter={12}>
            <Col xs={24} sm={14}>
              <Form.Item name="phone" label={t('common.phoneWhatsapp')} rules={[{ required: true, message: t('common.phoneRequired') }]}>
                <Input placeholder="+1 555 123 4567" inputMode="tel" />
              </Form.Item>
            </Col>
            <Col xs={24} sm={10}>
              <Form.Item name="currency" label={t('common.currencyCode')}>
                <Input maxLength={3} placeholder="USD" />
              </Form.Item>
            </Col>
          </Row>
          <Form.Item name="description" label={t('common.description')}>
            <Input.TextArea rows={4} />
          </Form.Item>
          <Form.Item label={t('common.logo')}>
            <ImageUpload value={logoUrl} onChange={setLogoUrl} buttonText={t('common.uploadLogo')} />
          </Form.Item>
          <Form.Item
            name="storefrontAccess"
            label={t('storeProfile.access')}
            extra={
              <Space size={4} wrap>
                {t('storeProfile.accessExtra')}
                <Link to="/store/customers">{t('storeProfile.accessExtraLink')}</Link>
              </Space>
            }
          >
            <Radio.Group>
              <Space direction="vertical" size={4}>
                <AccessOption
                  value="Public"
                  title={t('storeProfile.accessPublic')}
                  description={t('storeProfile.accessPublicHint')}
                />
                <AccessOption
                  value="InviteOnly"
                  title={t('storeProfile.accessInviteOnly')}
                  description={t('storeProfile.accessInviteOnlyHint')}
                />
              </Space>
            </Radio.Group>
          </Form.Item>
          <Button type="primary" htmlType="submit" block={isMobile} loading={save.isPending}>
            {t('storeProfile.saveChanges')}
          </Button>
        </Form>
      </Card>
    </div>
  );
}
