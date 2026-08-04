import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { App as AntApp, Button, Card, Form, Input, Typography } from 'antd';
import { api } from '../../api';
import ImageUpload from '../../components/ImageUpload';

interface ProfileValues {
  name: string;
  phone: string;
  currency: string;
  description?: string;
}

export default function StoreProfilePage() {
  const { t } = useTranslation();
  const qc = useQueryClient();
  const { message } = AntApp.useApp();
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
      }),
    onSuccess: () => {
      message.success(t('storeProfile.updated'));
      qc.invalidateQueries({ queryKey: ['backoffice', 'store'] });
    },
    onError: () => message.error(t('storeProfile.saveError')),
  });

  return (
    <div style={{ maxWidth: 640 }}>
      <Typography.Title level={3}>{t('storeProfile.title')}</Typography.Title>
      <Card loading={isLoading}>
        <Form form={form} layout="vertical" onFinish={(v) => save.mutate(v)}>
          <Form.Item name="name" label={t('storeProfile.storeName')} rules={[{ required: true, message: t('common.nameRequired') }]}>
            <Input />
          </Form.Item>
          <Form.Item name="phone" label={t('common.phoneWhatsapp')} rules={[{ required: true, message: t('common.phoneRequired') }]}>
            <Input placeholder="+1 555 123 4567" />
          </Form.Item>
          <Form.Item name="currency" label={t('common.currencyCode')}>
            <Input maxLength={3} placeholder="USD" />
          </Form.Item>
          <Form.Item name="description" label={t('common.description')}>
            <Input.TextArea rows={4} />
          </Form.Item>
          <Form.Item label={t('common.logo')}>
            <ImageUpload value={logoUrl} onChange={setLogoUrl} buttonText={t('common.uploadLogo')} />
          </Form.Item>
          {data && (
            <Typography.Paragraph type="secondary">
              {t('storeProfile.publicUrl')}: <code>/{data.slug}</code>
            </Typography.Paragraph>
          )}
          <Button type="primary" htmlType="submit" loading={save.isPending}>
            {t('storeProfile.saveChanges')}
          </Button>
        </Form>
      </Card>
    </div>
  );
}
