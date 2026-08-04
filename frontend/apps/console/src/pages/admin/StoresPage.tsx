import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import {
  App as AntApp,
  Avatar,
  Button,
  Form,
  Input,
  Modal,
  Popconfirm,
  Space,
  Switch,
  Table,
  Tag,
  Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { PlusOutlined } from '@ant-design/icons';
import type { StoreListItem } from '@gentlestore/shared';
import { resolveAssetUrl } from '@gentlestore/shared';
import { api, API_URL } from '../../api';
import ImageUpload from '../../components/ImageUpload';

const STOREFRONT_URL = 'http://localhost:5174';

interface StoreFormValues {
  name: string;
  slug?: string;
  phone: string;
  currency: string;
  description?: string;
  isActive: boolean;
}

export default function StoresPage() {
  const { t } = useTranslation();
  const qc = useQueryClient();
  const { message } = AntApp.useApp();
  const [open, setOpen] = useState(false);
  const [editing, setEditing] = useState<StoreListItem | null>(null);
  const [logoUrl, setLogoUrl] = useState<string | undefined>();
  const [form] = Form.useForm<StoreFormValues>();

  const { data: stores, isLoading } = useQuery({ queryKey: ['admin', 'stores'], queryFn: () => api.admin.listStores() });
  const invalidate = () => qc.invalidateQueries({ queryKey: ['admin', 'stores'] });

  const saveMutation = useMutation({
    mutationFn: (values: StoreFormValues) => {
      const payload = {
        name: values.name,
        slug: values.slug,
        phone: values.phone,
        currency: values.currency || 'USD',
        description: values.description,
        logoUrl,
        isActive: values.isActive,
      };
      return editing ? api.admin.updateStore(editing.id, payload) : api.admin.createStore(payload);
    },
    onSuccess: () => {
      message.success(t('stores.saved'));
      setOpen(false);
      invalidate();
    },
    onError: () => message.error(t('stores.saveError')),
  });

  const toggleActive = useMutation({
    mutationFn: (s: StoreListItem) => (s.isActive ? api.admin.deactivateStore(s.id) : api.admin.activateStore(s.id)),
    onSuccess: invalidate,
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => api.admin.deleteStore(id),
    onSuccess: () => {
      message.success(t('stores.deleted'));
      invalidate();
    },
    onError: () => message.error(t('stores.deleteError')),
  });

  const openCreate = () => {
    setEditing(null);
    setLogoUrl(undefined);
    form.resetFields();
    form.setFieldsValue({ currency: 'USD', isActive: true });
    setOpen(true);
  };

  const openEdit = async (s: StoreListItem) => {
    const detail = await api.admin.getStore(s.id);
    setEditing(s);
    setLogoUrl(detail.logoUrl ?? undefined);
    form.setFieldsValue({
      name: detail.name,
      slug: detail.slug,
      phone: detail.phone,
      currency: detail.currency,
      description: detail.description ?? '',
      isActive: detail.isActive,
    });
    setOpen(true);
  };

  const columns: ColumnsType<StoreListItem> = [
    {
      title: t('stores.colStore'),
      key: 'store',
      render: (_, r) => (
        <Space>
          <Avatar src={resolveAssetUrl(API_URL, r.logoUrl)} shape="square">
            {r.name.charAt(0)}
          </Avatar>
          <div>
            <div style={{ fontWeight: 600 }}>{r.name}</div>
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              /{r.slug}
            </Typography.Text>
          </div>
        </Space>
      ),
    },
    { title: t('stores.colPhone'), dataIndex: 'phone', key: 'phone' },
    { title: t('stores.colProducts'), dataIndex: 'productCount', key: 'productCount' },
    {
      title: t('common.status'),
      key: 'status',
      render: (_, r) => (r.isActive ? <Tag color="green">{t('common.active')}</Tag> : <Tag>{t('common.inactive')}</Tag>),
    },
    {
      title: t('common.actions'),
      key: 'actions',
      render: (_, r) => (
        <Space wrap>
          <a href={`${STOREFRONT_URL}/${r.slug}`} target="_blank" rel="noreferrer">
            {t('stores.view')}
          </a>
          <Button size="small" onClick={() => openEdit(r)}>
            {t('common.edit')}
          </Button>
          <Button size="small" onClick={() => toggleActive.mutate(r)}>
            {r.isActive ? t('stores.deactivate') : t('stores.activate')}
          </Button>
          <Popconfirm title={t('stores.deleteConfirm')} onConfirm={() => deleteMutation.mutate(r.id)}>
            <Button size="small" danger>
              {t('common.delete')}
            </Button>
          </Popconfirm>
        </Space>
      ),
    },
  ];

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 16 }}>
        <Typography.Title level={3} style={{ margin: 0 }}>
          {t('stores.title')}
        </Typography.Title>
        <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
          {t('stores.new')}
        </Button>
      </div>

      <Table rowKey="id" loading={isLoading} dataSource={stores} columns={columns} pagination={false} />

      <Modal
        title={editing ? t('stores.edit') : t('stores.new')}
        open={open}
        onCancel={() => setOpen(false)}
        onOk={() => form.submit()}
        confirmLoading={saveMutation.isPending}
        destroyOnHidden
      >
        <Form form={form} layout="vertical" onFinish={(v) => saveMutation.mutate(v)}>
          <Form.Item name="name" label={t('common.name')} rules={[{ required: true, message: t('common.nameRequired') }]}>
            <Input />
          </Form.Item>
          <Form.Item name="slug" label={t('stores.slug')} tooltip={t('stores.slugTooltip')}>
            <Input placeholder={t('stores.slugPlaceholder')} />
          </Form.Item>
          <Form.Item name="phone" label={t('common.phoneWhatsapp')} rules={[{ required: true, message: t('common.phoneRequired') }]}>
            <Input placeholder="+1 555 123 4567" />
          </Form.Item>
          <Form.Item name="currency" label={t('common.currencyCode')}>
            <Input maxLength={3} placeholder="USD" />
          </Form.Item>
          <Form.Item name="description" label={t('common.description')}>
            <Input.TextArea rows={3} />
          </Form.Item>
          <Form.Item label={t('common.logo')}>
            <ImageUpload value={logoUrl} onChange={setLogoUrl} buttonText={t('common.uploadLogo')} />
          </Form.Item>
          <Form.Item name="isActive" label={t('stores.active')} valuePropName="checked">
            <Switch />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}
