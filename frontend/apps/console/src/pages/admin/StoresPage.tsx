import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
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
      message.success('Store saved');
      setOpen(false);
      invalidate();
    },
    onError: () => message.error('Could not save store'),
  });

  const toggleActive = useMutation({
    mutationFn: (s: StoreListItem) => (s.isActive ? api.admin.deactivateStore(s.id) : api.admin.activateStore(s.id)),
    onSuccess: invalidate,
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => api.admin.deleteStore(id),
    onSuccess: () => {
      message.success('Store deleted');
      invalidate();
    },
    onError: () => message.error('Could not delete store'),
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
      title: 'Store',
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
    { title: 'Phone', dataIndex: 'phone', key: 'phone' },
    { title: 'Products', dataIndex: 'productCount', key: 'productCount' },
    {
      title: 'Status',
      key: 'status',
      render: (_, r) => (r.isActive ? <Tag color="green">Active</Tag> : <Tag>Inactive</Tag>),
    },
    {
      title: 'Actions',
      key: 'actions',
      render: (_, r) => (
        <Space wrap>
          <a href={`${STOREFRONT_URL}/${r.slug}`} target="_blank" rel="noreferrer">
            View
          </a>
          <Button size="small" onClick={() => openEdit(r)}>
            Edit
          </Button>
          <Button size="small" onClick={() => toggleActive.mutate(r)}>
            {r.isActive ? 'Deactivate' : 'Activate'}
          </Button>
          <Popconfirm title="Delete this store and all its data?" onConfirm={() => deleteMutation.mutate(r.id)}>
            <Button size="small" danger>
              Delete
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
          Stores
        </Typography.Title>
        <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
          New store
        </Button>
      </div>

      <Table rowKey="id" loading={isLoading} dataSource={stores} columns={columns} pagination={false} />

      <Modal
        title={editing ? 'Edit store' : 'New store'}
        open={open}
        onCancel={() => setOpen(false)}
        onOk={() => form.submit()}
        confirmLoading={saveMutation.isPending}
        destroyOnHidden
      >
        <Form form={form} layout="vertical" onFinish={(v) => saveMutation.mutate(v)}>
          <Form.Item name="name" label="Name" rules={[{ required: true, message: 'Name is required' }]}>
            <Input />
          </Form.Item>
          <Form.Item name="slug" label="Slug" tooltip="Used in the public URL. Leave blank to auto-generate.">
            <Input placeholder="auto from name" />
          </Form.Item>
          <Form.Item name="phone" label="Phone (WhatsApp)" rules={[{ required: true, message: 'Phone is required' }]}>
            <Input placeholder="+1 555 123 4567" />
          </Form.Item>
          <Form.Item name="currency" label="Currency code">
            <Input maxLength={3} placeholder="USD" />
          </Form.Item>
          <Form.Item name="description" label="Description">
            <Input.TextArea rows={3} />
          </Form.Item>
          <Form.Item label="Logo">
            <ImageUpload value={logoUrl} onChange={setLogoUrl} buttonText="Upload logo" />
          </Form.Item>
          <Form.Item name="isActive" label="Active" valuePropName="checked">
            <Switch />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}
