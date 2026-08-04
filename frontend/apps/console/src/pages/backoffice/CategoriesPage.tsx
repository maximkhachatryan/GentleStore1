import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { App as AntApp, Button, Form, Input, InputNumber, Modal, Popconfirm, Space, Table, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { PlusOutlined } from '@ant-design/icons';
import type { Category } from '@gentlestore/shared';
import { api } from '../../api';

interface CategoryValues {
  name: string;
  displayOrder: number;
}

export default function CategoriesPage() {
  const { t } = useTranslation();
  const qc = useQueryClient();
  const { message } = AntApp.useApp();
  const [open, setOpen] = useState(false);
  const [editing, setEditing] = useState<Category | null>(null);
  const [form] = Form.useForm<CategoryValues>();

  const { data, isLoading } = useQuery({ queryKey: ['backoffice', 'categories'], queryFn: () => api.backoffice.listCategories() });
  const invalidate = () => qc.invalidateQueries({ queryKey: ['backoffice', 'categories'] });

  const save = useMutation({
    mutationFn: (v: CategoryValues) =>
      editing ? api.backoffice.updateCategory(editing.id, v) : api.backoffice.createCategory(v),
    onSuccess: () => {
      message.success(t('categories.saved'));
      setOpen(false);
      invalidate();
    },
    onError: () => message.error(t('categories.saveError')),
  });

  const remove = useMutation({
    mutationFn: (id: string) => api.backoffice.deleteCategory(id),
    onSuccess: () => {
      message.success(t('categories.deleted'));
      invalidate();
    },
    onError: () => message.error(t('categories.deleteInUse')),
  });

  const openCreate = () => {
    setEditing(null);
    form.resetFields();
    form.setFieldsValue({ displayOrder: (data?.length ?? 0) + 1 });
    setOpen(true);
  };

  const openEdit = (c: Category) => {
    setEditing(c);
    form.setFieldsValue({ name: c.name, displayOrder: c.displayOrder });
    setOpen(true);
  };

  const columns: ColumnsType<Category> = [
    { title: t('categories.colOrder'), dataIndex: 'displayOrder', key: 'displayOrder', width: 90 },
    { title: t('common.name'), dataIndex: 'name', key: 'name' },
    { title: t('categories.colProducts'), dataIndex: 'productCount', key: 'productCount', width: 120 },
    {
      title: t('common.actions'),
      key: 'actions',
      width: 180,
      render: (_, r) => (
        <Space>
          <Button size="small" onClick={() => openEdit(r)}>
            {t('common.edit')}
          </Button>
          <Popconfirm title={t('categories.deleteConfirm')} onConfirm={() => remove.mutate(r.id)}>
            <Button size="small" danger>
              {t('common.delete')}
            </Button>
          </Popconfirm>
        </Space>
      ),
    },
  ];

  return (
    <div style={{ maxWidth: 760 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 16 }}>
        <Typography.Title level={3} style={{ margin: 0 }}>
          {t('categories.title')}
        </Typography.Title>
        <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
          {t('categories.new')}
        </Button>
      </div>

      <Table rowKey="id" loading={isLoading} dataSource={data} columns={columns} pagination={false} />

      <Modal
        title={editing ? t('categories.edit') : t('categories.new')}
        open={open}
        onCancel={() => setOpen(false)}
        onOk={() => form.submit()}
        confirmLoading={save.isPending}
        destroyOnHidden
      >
        <Form form={form} layout="vertical" onFinish={(v) => save.mutate(v)}>
          <Form.Item name="name" label={t('common.name')} rules={[{ required: true, message: t('common.nameRequired') }]}>
            <Input />
          </Form.Item>
          <Form.Item name="displayOrder" label={t('common.displayOrder')} rules={[{ required: true }]}>
            <InputNumber min={0} style={{ width: '100%' }} />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}
