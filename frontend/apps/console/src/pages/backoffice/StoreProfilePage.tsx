import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
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
      message.success('Profile updated');
      qc.invalidateQueries({ queryKey: ['backoffice', 'store'] });
    },
    onError: () => message.error('Could not save profile'),
  });

  return (
    <div style={{ maxWidth: 640 }}>
      <Typography.Title level={3}>Store Profile</Typography.Title>
      <Card loading={isLoading}>
        <Form form={form} layout="vertical" onFinish={(v) => save.mutate(v)}>
          <Form.Item name="name" label="Store name" rules={[{ required: true, message: 'Name is required' }]}>
            <Input />
          </Form.Item>
          <Form.Item name="phone" label="Phone (WhatsApp)" rules={[{ required: true, message: 'Phone is required' }]}>
            <Input placeholder="+1 555 123 4567" />
          </Form.Item>
          <Form.Item name="currency" label="Currency code">
            <Input maxLength={3} placeholder="USD" />
          </Form.Item>
          <Form.Item name="description" label="Description">
            <Input.TextArea rows={4} />
          </Form.Item>
          <Form.Item label="Logo">
            <ImageUpload value={logoUrl} onChange={setLogoUrl} buttonText="Upload logo" />
          </Form.Item>
          {data && (
            <Typography.Paragraph type="secondary">
              Public URL: <code>/{data.slug}</code>
            </Typography.Paragraph>
          )}
          <Button type="primary" htmlType="submit" loading={save.isPending}>
            Save changes
          </Button>
        </Form>
      </Card>
    </div>
  );
}
