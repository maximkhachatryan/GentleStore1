import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { App as AntApp, Button, Form, Input, InputNumber, Popconfirm, Space, Tag } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { PlusOutlined } from '@ant-design/icons';
import type { Tag as TagModel } from '@gentlestore/shared';
import { api } from '../../api';
import EntityCard from '../../components/EntityCard';
import FormDialog from '../../components/FormDialog';
import PageHeader from '../../components/PageHeader';
import ResponsiveTable from '../../components/ResponsiveTable';

interface TagValues {
  name: string;
  displayOrder: number;
}

export default function TagsPage() {
  const { t } = useTranslation();
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
      message.success(t('tags.saved'));
      setOpen(false);
      invalidate();
    },
    onError: () => message.error(t('tags.saveError')),
  });

  const remove = useMutation({
    mutationFn: (id: string) => api.backoffice.deleteTag(id),
    onSuccess: () => {
      message.success(t('tags.deleted'));
      invalidate();
    },
    onError: () => message.error(t('tags.deleteError')),
  });

  const openCreate = () => {
    setEditing(null);
    form.resetFields();
    form.setFieldsValue({ displayOrder: (data?.length ?? 0) + 1 });
    setOpen(true);
  };

  const openEdit = (tag: TagModel) => {
    setEditing(tag);
    form.setFieldsValue({ name: tag.name, displayOrder: tag.displayOrder });
    setOpen(true);
  };

  const rowActions = (tag: TagModel) => (
    <>
      <Button size="small" onClick={() => openEdit(tag)}>
        {t('common.edit')}
      </Button>
      <Popconfirm title={t('tags.deleteConfirm')} onConfirm={() => remove.mutate(tag.id)}>
        <Button size="small" danger>
          {t('common.delete')}
        </Button>
      </Popconfirm>
    </>
  );

  const columns: ColumnsType<TagModel> = [
    { title: t('tags.colOrder'), dataIndex: 'displayOrder', key: 'displayOrder', width: 90 },
    { title: t('common.name'), dataIndex: 'name', key: 'name' },
    {
      title: t('common.actions'),
      key: 'actions',
      width: 180,
      align: 'end',
      fixed: 'right',
      render: (_, r) => <Space>{rowActions(r)}</Space>,
    },
  ];

  return (
    <div style={{ maxWidth: 640 }}>
      <PageHeader
        title={t('tags.title')}
        actions={
          <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
            {t('tags.new')}
          </Button>
        }
      />

      <ResponsiveTable<TagModel>
        rowKey="id"
        loading={isLoading}
        dataSource={data}
        columns={columns}
        pagination={false}
        cardKey={(r) => r.id}
        renderCard={(r) => (
          <EntityCard
            title={r.name}
            extra={<Tag style={{ marginInlineEnd: 0 }}>#{r.displayOrder}</Tag>}
            actions={rowActions(r)}
          />
        )}
      />

      <FormDialog
        title={editing ? t('tags.edit') : t('tags.new')}
        open={open}
        onCancel={() => setOpen(false)}
        onOk={() => form.submit()}
        confirmLoading={save.isPending}
      >
        <Form form={form} layout="vertical" onFinish={(v) => save.mutate(v)}>
          <Form.Item name="name" label={t('common.name')} rules={[{ required: true, message: t('common.nameRequired') }]}>
            <Input />
          </Form.Item>
          <Form.Item name="displayOrder" label={t('common.displayOrder')} rules={[{ required: true }]}>
            <InputNumber min={0} style={{ width: '100%' }} />
          </Form.Item>
        </Form>
      </FormDialog>
    </div>
  );
}
