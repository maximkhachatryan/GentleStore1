import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
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
      message.success('Category saved');
      setOpen(false);
      invalidate();
    },
    onError: () => message.error('Could not save category'),
  });

  const remove = useMutation({
    mutationFn: (id: string) => api.backoffice.deleteCategory(id),
    onSuccess: () => {
      message.success('Category deleted');
      invalidate();
    },
    onError: () => message.error('Cannot delete a category that still has products'),
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
    { title: 'Order', dataIndex: 'displayOrder', key: 'displayOrder', width: 90 },
    { title: 'Name', dataIndex: 'name', key: 'name' },
    { title: 'Products', dataIndex: 'productCount', key: 'productCount', width: 120 },
    {
      title: 'Actions',
      key: 'actions',
      width: 180,
      render: (_, r) => (
        <Space>
          <Button size="small" onClick={() => openEdit(r)}>
            Edit
          </Button>
          <Popconfirm title="Delete this category?" onConfirm={() => remove.mutate(r.id)}>
            <Button size="small" danger>
              Delete
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
          Categories
        </Typography.Title>
        <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
          New category
        </Button>
      </div>

      <Table rowKey="id" loading={isLoading} dataSource={data} columns={columns} pagination={false} />

      <Modal
        title={editing ? 'Edit category' : 'New category'}
        open={open}
        onCancel={() => setOpen(false)}
        onOk={() => form.submit()}
        confirmLoading={save.isPending}
        destroyOnHidden
      >
        <Form form={form} layout="vertical" onFinish={(v) => save.mutate(v)}>
          <Form.Item name="name" label="Name" rules={[{ required: true, message: 'Name is required' }]}>
            <Input />
          </Form.Item>
          <Form.Item name="displayOrder" label="Display order" rules={[{ required: true }]}>
            <InputNumber min={0} style={{ width: '100%' }} />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}
