import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { App as AntApp, Button, Form, Input, InputNumber, Modal, Popconfirm, Space, Table, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { PlusOutlined } from '@ant-design/icons';
import type { Tag as TagModel } from '@gentlestore/shared';
import { api } from '../../api';

interface TagValues {
  name: string;
  displayOrder: number;
}

export default function TagsPage() {
  const qc = useQueryClient();
  const { message } = AntApp.useApp();
  const [open, setOpen] = useState(false);
  const [editing, setEditing] = useState<TagModel | null>(null);
  const [form] = Form.useForm<TagValues>();

  const { data, isLoading } = useQuery({ queryKey: ['backoffice', 'tags'], queryFn: () => api.backoffice.listTags() });
  const invalidate = () => qc.invalidateQueries({ queryKey: ['backoffice', 'tags'] });

  const save = useMutation({
    mutationFn: (v: TagValues) => (editing ? api.backoffice.updateTag(editing.id, v) : api.backoffice.createTag(v)),
    onSuccess: () => {
      message.success('Tag saved');
      setOpen(false);
      invalidate();
    },
    onError: () => message.error('Could not save tag. The name may already exist.'),
  });

  const remove = useMutation({
    mutationFn: (id: string) => api.backoffice.deleteTag(id),
    onSuccess: () => {
      message.success('Tag deleted');
      invalidate();
    },
    onError: () => message.error('Could not delete tag'),
  });

  const openCreate = () => {
    setEditing(null);
    form.resetFields();
    form.setFieldsValue({ displayOrder: (data?.length ?? 0) + 1 });
    setOpen(true);
  };

  const openEdit = (t: TagModel) => {
    setEditing(t);
    form.setFieldsValue({ name: t.name, displayOrder: t.displayOrder });
    setOpen(true);
  };

  const columns: ColumnsType<TagModel> = [
    { title: 'Order', dataIndex: 'displayOrder', key: 'displayOrder', width: 90 },
    { title: 'Name', dataIndex: 'name', key: 'name' },
    {
      title: 'Actions',
      key: 'actions',
      width: 180,
      render: (_, r) => (
        <Space>
          <Button size="small" onClick={() => openEdit(r)}>
            Edit
          </Button>
          <Popconfirm title="Delete this tag?" onConfirm={() => remove.mutate(r.id)}>
            <Button size="small" danger>
              Delete
            </Button>
          </Popconfirm>
        </Space>
      ),
    },
  ];

  return (
    <div style={{ maxWidth: 640 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 16 }}>
        <Typography.Title level={3} style={{ margin: 0 }}>
          Tags
        </Typography.Title>
        <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
          New tag
        </Button>
      </div>

      <Table rowKey="id" loading={isLoading} dataSource={data} columns={columns} pagination={false} />

      <Modal
        title={editing ? 'Edit tag' : 'New tag'}
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
