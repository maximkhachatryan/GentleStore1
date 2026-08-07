import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { App as AntApp, Button, Form, Input, Popconfirm, Select, Space, Switch, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { PlusOutlined } from '@ant-design/icons';
import type { AdminUser, UserRole } from '@gentlestore/shared';
import { api } from '../../api';
import EntityCard from '../../components/EntityCard';
import FormDialog from '../../components/FormDialog';
import PageHeader from '../../components/PageHeader';
import ResponsiveTable from '../../components/ResponsiveTable';

interface UserFormValues {
  email: string;
  fullName: string;
  password?: string;
  role: UserRole;
  storeId?: string;
  isActive: boolean;
}

const ROLE_LABELS: Record<UserRole, string> = {
  SuperAdmin: 'users.roleSuperAdmin',
  StoreOwner: 'users.roleStoreOwner',
  StoreStaff: 'users.roleStoreStaff',
};

export default function UsersPage() {
  const { t } = useTranslation();
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
      message.success(t('users.saved'));
      setOpen(false);
      invalidate();
    },
    onError: () => message.error(t('users.saveError')),
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => api.admin.deleteUser(id),
    onSuccess: () => {
      message.success(t('users.deleted'));
      invalidate();
    },
    onError: () => message.error(t('users.deleteError')),
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

  const roleTag = (u: AdminUser) => (
    <Tag color={u.role === 'SuperAdmin' ? 'purple' : 'blue'} style={{ marginInlineEnd: 0 }}>
      {t(ROLE_LABELS[u.role])}
    </Tag>
  );

  const statusTag = (u: AdminUser) =>
    u.isActive ? <Tag color="green">{t('common.active')}</Tag> : <Tag>{t('common.disabled')}</Tag>;

  const rowActions = (u: AdminUser) => (
    <>
      <Button size="small" onClick={() => openEdit(u)}>
        {t('common.edit')}
      </Button>
      <Popconfirm title={t('users.deleteConfirm')} onConfirm={() => deleteMutation.mutate(u.id)}>
        <Button size="small" danger>
          {t('common.delete')}
        </Button>
      </Popconfirm>
    </>
  );

  const columns: ColumnsType<AdminUser> = [
    { title: t('users.fullName'), dataIndex: 'fullName', key: 'fullName' },
    { title: t('users.email'), dataIndex: 'email', key: 'email' },
    { title: t('users.role'), key: 'role', render: (_, r) => roleTag(r) },
    { title: t('users.store'), key: 'store', render: (_, r) => r.storeName ?? <Typography.Text type="secondary">—</Typography.Text> },
    { title: t('common.status'), key: 'status', render: (_, r) => statusTag(r) },
    {
      title: t('common.actions'),
      key: 'actions',
      align: 'end',
      fixed: 'right',
      render: (_, r) => <Space>{rowActions(r)}</Space>,
    },
  ];

  return (
    <div>
      <PageHeader
        title={t('users.title')}
        actions={
          <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
            {t('users.new')}
          </Button>
        }
      />

      <ResponsiveTable<AdminUser>
        rowKey="id"
        loading={isLoading}
        dataSource={users}
        columns={columns}
        pagination={false}
        cardKey={(r) => r.id}
        renderCard={(r) => (
          <EntityCard
            title={r.fullName}
            subtitle={r.email}
            fields={[
              { label: t('users.role'), value: roleTag(r) },
              { label: t('users.store'), value: r.storeName ?? <Typography.Text type="secondary">—</Typography.Text> },
              { label: t('common.status'), value: statusTag(r) },
            ]}
            actions={rowActions(r)}
          />
        )}
      />

      <FormDialog
        title={editing ? t('users.edit') : t('users.new')}
        open={open}
        onCancel={() => setOpen(false)}
        onOk={() => form.submit()}
        confirmLoading={saveMutation.isPending}
      >
        <Form form={form} layout="vertical" onFinish={(v) => saveMutation.mutate(v)}>
          <Form.Item name="email" label={t('users.email')} rules={[{ required: true, type: 'email', message: t('users.validEmail') }]}>
            <Input disabled={!!editing} inputMode="email" autoComplete="off" />
          </Form.Item>
          <Form.Item name="fullName" label={t('users.fullName')} rules={[{ required: true, message: t('common.nameRequired') }]}>
            <Input />
          </Form.Item>
          <Form.Item
            name="password"
            label={editing ? t('users.newPassword') : t('users.password')}
            rules={editing ? [] : [{ required: true, min: 6, message: t('users.minPassword') }]}
          >
            <Input.Password autoComplete="new-password" />
          </Form.Item>
          <Form.Item name="role" label={t('users.role')} rules={[{ required: true }]}>
            <Select
              options={[
                { label: t('users.roleSuperAdmin'), value: 'SuperAdmin' },
                { label: t('users.roleStoreOwner'), value: 'StoreOwner' },
                { label: t('users.roleStoreStaff'), value: 'StoreStaff' },
              ]}
            />
          </Form.Item>
          {role !== 'SuperAdmin' && (
            <Form.Item name="storeId" label={t('users.store')} rules={[{ required: true, message: t('users.storeRequired') }]}>
              <Select options={storeOptions} placeholder={t('users.selectStore')} showSearch optionFilterProp="label" />
            </Form.Item>
          )}
          {editing && (
            <Form.Item name="isActive" label={t('common.active')} valuePropName="checked">
              <Switch />
            </Form.Item>
          )}
        </Form>
      </FormDialog>
    </div>
  );
}
