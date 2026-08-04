import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  App as AntApp,
  Button,
  Form,
  Input,
  Modal,
  Popconfirm,
  Select,
  Space,
  Switch,
  Table,
  Tag,
  Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { PlusOutlined } from '@ant-design/icons';
import type { AdminUser, UserRole } from '@gentlestore/shared';
import { api } from '../../api';

interface UserFormValues {
  email: string;
  fullName: string;
  password?: string;
  role: UserRole;
  storeId?: string;
  isActive: boolean;
}

const ROLE_LABELS: Record<UserRole, string> = {
  SuperAdmin: 'Super Admin',
  StoreOwner: 'Store Owner',
  StoreStaff: 'Store Staff',
};

export default function UsersPage() {
  const qc = useQueryClient();
  const { message } = AntApp.useApp();
  const [open, setOpen] = useState(false);
  const [editing, setEditing] = useState<AdminUser | null>(null);
  const [form] = Form.useForm<UserFormValues>();
  const role = Form.useWatch('role', form);

  const { data: users, isLoading } = useQuery({ queryKey: ['admin', 'users'], queryFn: () => api.admin.listUsers() });
  const { data: stores } = useQuery({ queryKey: ['admin', 'stores'], queryFn: () => api.admin.listStores() });

  const invalidate = () => qc.invalidateQueries({ queryKey: ['admin', 'users'] });

  const storeOptions = useMemo(
    () => (stores ?? []).map((s) => ({ label: s.name, value: s.id })),
    [stores],
  );

  const saveMutation = useMutation({
    mutationFn: (values: UserFormValues) => {
      const storeId = values.role === 'SuperAdmin' ? null : values.storeId;
      if (editing) {
        return api.admin.updateUser(editing.id, {
          fullName: values.fullName,
          password: values.password || null,
          isActive: values.isActive,
          role: values.role,
          storeId,
        });
      }
      return api.admin.createUser({
        email: values.email,
        fullName: values.fullName,
        password: values.password ?? '',
        role: values.role,
        storeId,
      });
    },
    onSuccess: () => {
      message.success('User saved');
      setOpen(false);
      invalidate();
    },
    onError: () => message.error('Could not save user. Email may already be in use.'),
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => api.admin.deleteUser(id),
    onSuccess: () => {
      message.success('User deleted');
      invalidate();
    },
    onError: () => message.error('Could not delete user'),
  });

  const openCreate = () => {
    setEditing(null);
    form.resetFields();
    form.setFieldsValue({ role: 'StoreOwner', isActive: true });
    setOpen(true);
  };

  const openEdit = (u: AdminUser) => {
    setEditing(u);
    form.resetFields();
    form.setFieldsValue({
      email: u.email,
      fullName: u.fullName,
      role: u.role,
      storeId: u.storeId ?? undefined,
      isActive: u.isActive,
    });
    setOpen(true);
  };

  const columns: ColumnsType<AdminUser> = [
    { title: 'Name', dataIndex: 'fullName', key: 'fullName' },
    { title: 'Email', dataIndex: 'email', key: 'email' },
    { title: 'Role', key: 'role', render: (_, r) => <Tag color={r.role === 'SuperAdmin' ? 'purple' : 'blue'}>{ROLE_LABELS[r.role]}</Tag> },
    { title: 'Store', key: 'store', render: (_, r) => r.storeName ?? <Typography.Text type="secondary">—</Typography.Text> },
    { title: 'Status', key: 'status', render: (_, r) => (r.isActive ? <Tag color="green">Active</Tag> : <Tag>Disabled</Tag>) },
    {
      title: 'Actions',
      key: 'actions',
      render: (_, r) => (
        <Space>
          <Button size="small" onClick={() => openEdit(r)}>
            Edit
          </Button>
          <Popconfirm title="Delete this user?" onConfirm={() => deleteMutation.mutate(r.id)}>
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
          Users
        </Typography.Title>
        <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
          New user
        </Button>
      </div>

      <Table rowKey="id" loading={isLoading} dataSource={users} columns={columns} pagination={false} />

      <Modal
        title={editing ? 'Edit user' : 'New user'}
        open={open}
        onCancel={() => setOpen(false)}
        onOk={() => form.submit()}
        confirmLoading={saveMutation.isPending}
        destroyOnHidden
      >
        <Form form={form} layout="vertical" onFinish={(v) => saveMutation.mutate(v)}>
          <Form.Item name="email" label="Email" rules={[{ required: true, type: 'email', message: 'Valid email required' }]}>
            <Input disabled={!!editing} />
          </Form.Item>
          <Form.Item name="fullName" label="Full name" rules={[{ required: true, message: 'Name is required' }]}>
            <Input />
          </Form.Item>
          <Form.Item
            name="password"
            label={editing ? 'New password (leave blank to keep)' : 'Password'}
            rules={editing ? [] : [{ required: true, min: 6, message: 'At least 6 characters' }]}
          >
            <Input.Password autoComplete="new-password" />
          </Form.Item>
          <Form.Item name="role" label="Role" rules={[{ required: true }]}>
            <Select
              options={[
                { label: 'Super Admin', value: 'SuperAdmin' },
                { label: 'Store Owner', value: 'StoreOwner' },
                { label: 'Store Staff', value: 'StoreStaff' },
              ]}
            />
          </Form.Item>
          {role !== 'SuperAdmin' && (
            <Form.Item name="storeId" label="Store" rules={[{ required: true, message: 'Store is required for store roles' }]}>
              <Select options={storeOptions} placeholder="Select a store" showSearch optionFilterProp="label" />
            </Form.Item>
          )}
          {editing && (
            <Form.Item name="isActive" label="Active" valuePropName="checked">
              <Switch />
            </Form.Item>
          )}
        </Form>
      </Modal>
    </div>
  );
}
