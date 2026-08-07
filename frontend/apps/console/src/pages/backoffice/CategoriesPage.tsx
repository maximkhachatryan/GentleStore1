import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { App as AntApp, Button, Form, Input, InputNumber, Popconfirm, Space, Tag } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { PlusOutlined } from '@ant-design/icons';
import type { Category } from '@gentlestore/shared';
import { api } from '../../api';
import EntityCard from '../../components/EntityCard';
import FormDialog from '../../components/FormDialog';
import PageHeader from '../../components/PageHeader';
import ResponsiveTable from '../../components/ResponsiveTable';

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

  const rowActions = (c: Category) => (
    <>
      <Button size="small" onClick={() => openEdit(c)}>
        {t('common.edit')}
      </Button>
      <Popconfirm title={t('categories.deleteConfirm')} onConfirm={() => remove.mutate(c.id)}>
        <Button size="small" danger>
          {t('common.delete')}
        </Button>
      </Popconfirm>
    </>
  );

  const columns: ColumnsType<Category> = [
    { title: t('categories.colOrder'), dataIndex: 'displayOrder', key: 'displayOrder', width: 90 },
    { title: t('common.name'), dataIndex: 'name', key: 'name' },
    { title: t('categories.colProducts'), dataIndex: 'productCount', key: 'productCount', width: 120 },
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
    <div style={{ maxWidth: 760 }}>
      <PageHeader
        title={t('categories.title')}
        actions={
          <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
            {t('categories.new')}
          </Button>
        }
      />

      <ResponsiveTable<Category>
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
            fields={[{ label: t('categories.colProducts'), value: r.productCount }]}
            actions={rowActions(r)}
          />
        )}
      />

      <FormDialog
        title={editing ? t('categories.edit') : t('categories.new')}
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
