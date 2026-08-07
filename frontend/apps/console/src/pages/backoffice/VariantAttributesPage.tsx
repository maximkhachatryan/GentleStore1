import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { App as AntApp, Button, Form, Input, InputNumber, Popconfirm, Space, Table, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { PlusOutlined } from '@ant-design/icons';
import type { VariantAttributeDefinition, VariantAttributeOption } from '@gentlestore/shared';
import { api } from '../../api';
import EntityCard from '../../components/EntityCard';
import FormDialog from '../../components/FormDialog';
import PageHeader from '../../components/PageHeader';
import ResponsiveTable from '../../components/ResponsiveTable';

interface DefinitionValues {
  name: string;
  displayOrder: number;
}

interface OptionValues {
  value: string;
  displayOrder: number;
}

export default function VariantAttributesPage() {
  const { t } = useTranslation();
  const qc = useQueryClient();
  const { message, modal } = AntApp.useApp();

  const [defOpen, setDefOpen] = useState(false);
  const [editingDef, setEditingDef] = useState<VariantAttributeDefinition | null>(null);
  const [defForm] = Form.useForm<DefinitionValues>();

  const [optOpen, setOptOpen] = useState(false);
  const [optionParent, setOptionParent] = useState<VariantAttributeDefinition | null>(null);
  const [editingOption, setEditingOption] = useState<VariantAttributeOption | null>(null);
  const [optForm] = Form.useForm<OptionValues>();

  const { data, isLoading } = useQuery({
    queryKey: ['backoffice', 'variant-attributes'],
    queryFn: () => api.backoffice.listVariantAttributes(),
  });
  const invalidate = () => qc.invalidateQueries({ queryKey: ['backoffice', 'variant-attributes'] });

  const saveDef = useMutation({
    mutationFn: (v: DefinitionValues) =>
      editingDef ? api.backoffice.updateVariantAttribute(editingDef.id, v) : api.backoffice.createVariantAttribute(v),
    onSuccess: () => {
      message.success(t('variantAttributes.saved'));
      setDefOpen(false);
      invalidate();
    },
    onError: () => message.error(t('variantAttributes.saveError')),
  });

  const removeDef = useMutation({
    mutationFn: (id: string) => api.backoffice.deleteVariantAttribute(id),
    onSuccess: () => {
      message.success(t('variantAttributes.deleted'));
      invalidate();
    },
    onError: () => message.error(t('variantAttributes.deleteError')),
  });

  const saveOption = useMutation({
    mutationFn: (v: OptionValues) =>
      editingOption
        ? api.backoffice.updateVariantAttributeOption(optionParent!.id, editingOption.id, v)
        : api.backoffice.addVariantAttributeOption(optionParent!.id, v),
    onSuccess: () => {
      message.success(t('variantAttributes.optionSaved'));
      setOptOpen(false);
      invalidate();
    },
    onError: () => message.error(t('variantAttributes.optionSaveError')),
  });

  const removeOption = useMutation({
    mutationFn: (vars: { defId: string; optionId: string }) =>
      api.backoffice.deleteVariantAttributeOption(vars.defId, vars.optionId),
    onSuccess: () => {
      message.success(t('variantAttributes.optionDeleted'));
      invalidate();
    },
    onError: () => message.error(t('variantAttributes.optionDeleteError')),
  });

  const openCreateDef = () => {
    setEditingDef(null);
    defForm.resetFields();
    defForm.setFieldsValue({ displayOrder: (data?.length ?? 0) + 1 });
    setDefOpen(true);
  };

  const openEditDef = (d: VariantAttributeDefinition) => {
    setEditingDef(d);
    defForm.setFieldsValue({ name: d.name, displayOrder: d.displayOrder });
    setDefOpen(true);
  };

  const openCreateOption = (parent: VariantAttributeDefinition) => {
    setOptionParent(parent);
    setEditingOption(null);
    optForm.resetFields();
    optForm.setFieldsValue({ displayOrder: (parent.options.length ?? 0) + 1 });
    setOptOpen(true);
  };

  const openEditOption = (parent: VariantAttributeDefinition, option: VariantAttributeOption) => {
    setOptionParent(parent);
    setEditingOption(option);
    optForm.setFieldsValue({ value: option.value, displayOrder: option.displayOrder });
    setOptOpen(true);
  };

  const confirmRemoveOption = (parent: VariantAttributeDefinition, option: VariantAttributeOption) =>
    modal.confirm({
      title: t('variantAttributes.optionDeleteConfirm'),
      content: option.value,
      okText: t('common.delete'),
      okButtonProps: { danger: true },
      cancelText: t('common.cancel'),
      onOk: () => removeOption.mutate({ defId: parent.id, optionId: option.id }),
    });

  const columns: ColumnsType<VariantAttributeDefinition> = [
    { title: t('variantAttributes.colOrder'), dataIndex: 'displayOrder', key: 'displayOrder', width: 90 },
    { title: t('common.name'), dataIndex: 'name', key: 'name' },
    {
      title: t('variantAttributes.values'),
      key: 'values',
      render: (_, r) =>
        r.options.length ? (
          <Space wrap size={[4, 4]}>
            {r.options.map((o) => (
              <Tag key={o.id}>{o.value}</Tag>
            ))}
          </Space>
        ) : (
          <Typography.Text type="secondary">{t('variantAttributes.noValues')}</Typography.Text>
        ),
    },
    {
      title: t('common.actions'),
      key: 'actions',
      width: 280,
      align: 'end',
      fixed: 'right',
      render: (_, r) => (
        <Space>
          <Button size="small" icon={<PlusOutlined />} onClick={() => openCreateOption(r)}>
            {t('variantAttributes.addValue')}
          </Button>
          <Button size="small" onClick={() => openEditDef(r)}>
            {t('common.edit')}
          </Button>
          <Popconfirm title={t('variantAttributes.deleteConfirm')} onConfirm={() => removeDef.mutate(r.id)}>
            <Button size="small" danger>
              {t('common.delete')}
            </Button>
          </Popconfirm>
        </Space>
      ),
    },
  ];

  const optionColumns: ColumnsType<VariantAttributeOption> = [
    { title: t('variantAttributes.colOrder'), dataIndex: 'displayOrder', key: 'displayOrder', width: 90 },
    { title: t('variantAttributes.value'), dataIndex: 'value', key: 'value' },
    {
      title: t('common.actions'),
      key: 'actions',
      width: 180,
      align: 'end',
      render: (_, r) => {
        const parent = data?.find((d) => d.options.some((o) => o.id === r.id));
        if (!parent) return null;
        return (
          <Space>
            <Button size="small" onClick={() => openEditOption(parent, r)}>
              {t('common.edit')}
            </Button>
            <Popconfirm
              title={t('variantAttributes.optionDeleteConfirm')}
              onConfirm={() => removeOption.mutate({ defId: parent.id, optionId: r.id })}
            >
              <Button size="small" danger>
                {t('common.delete')}
              </Button>
            </Popconfirm>
          </Space>
        );
      },
    },
  ];

  return (
    <div style={{ maxWidth: 900 }}>
      <PageHeader
        title={t('variantAttributes.title')}
        subtitle={t('variantAttributes.hint')}
        actions={
          <Button type="primary" icon={<PlusOutlined />} onClick={openCreateDef}>
            {t('variantAttributes.new')}
          </Button>
        }
      />

      <ResponsiveTable<VariantAttributeDefinition>
        rowKey="id"
        loading={isLoading}
        dataSource={data}
        columns={columns}
        pagination={false}
        expandable={{
          expandedRowRender: (record) => (
            <Table
              rowKey="id"
              size="small"
              dataSource={record.options}
              columns={optionColumns}
              pagination={false}
              locale={{ emptyText: t('variantAttributes.noValues') }}
            />
          ),
          rowExpandable: (record) => record.options.length > 0,
        }}
        cardKey={(r) => r.id}
        renderCard={(r) => (
          <EntityCard
            title={r.name}
            extra={<Tag style={{ marginInlineEnd: 0 }}>#{r.displayOrder}</Tag>}
            actions={
              <>
                <Button size="small" icon={<PlusOutlined />} onClick={() => openCreateOption(r)}>
                  {t('variantAttributes.addValue')}
                </Button>
                <Button size="small" onClick={() => openEditDef(r)}>
                  {t('common.edit')}
                </Button>
                <Popconfirm title={t('variantAttributes.deleteConfirm')} onConfirm={() => removeDef.mutate(r.id)}>
                  <Button size="small" danger>
                    {t('common.delete')}
                  </Button>
                </Popconfirm>
              </>
            }
          >
            {r.options.length ? (
              <Space wrap size={[6, 6]}>
                {r.options.map((o) => (
                  <Tag
                    key={o.id}
                    closable
                    style={{ marginInlineEnd: 0, cursor: 'pointer', paddingBlock: 3 }}
                    onClick={() => openEditOption(r, o)}
                    onClose={(e) => {
                      e.preventDefault();
                      confirmRemoveOption(r, o);
                    }}
                  >
                    {o.value}
                  </Tag>
                ))}
              </Space>
            ) : (
              <Typography.Text type="secondary" style={{ fontSize: 13 }}>
                {t('variantAttributes.noValues')}
              </Typography.Text>
            )}
          </EntityCard>
        )}
      />

      <FormDialog
        title={editingDef ? t('variantAttributes.edit') : t('variantAttributes.new')}
        open={defOpen}
        onCancel={() => setDefOpen(false)}
        onOk={() => defForm.submit()}
        confirmLoading={saveDef.isPending}
      >
        <Form form={defForm} layout="vertical" onFinish={(v) => saveDef.mutate(v)}>
          <Form.Item name="name" label={t('common.name')} rules={[{ required: true, message: t('common.nameRequired') }]}>
            <Input placeholder={t('variantAttributes.namePlaceholder')} />
          </Form.Item>
          <Form.Item name="displayOrder" label={t('common.displayOrder')} rules={[{ required: true }]}>
            <InputNumber min={0} style={{ width: '100%' }} />
          </Form.Item>
        </Form>
      </FormDialog>

      <FormDialog
        title={editingOption ? t('variantAttributes.editValue') : t('variantAttributes.addValueTo', { name: optionParent?.name })}
        open={optOpen}
        onCancel={() => setOptOpen(false)}
        onOk={() => optForm.submit()}
        confirmLoading={saveOption.isPending}
      >
        <Form form={optForm} layout="vertical" onFinish={(v) => saveOption.mutate(v)}>
          <Form.Item name="value" label={t('variantAttributes.value')} rules={[{ required: true, message: t('variantAttributes.valueRequired') }]}>
            <Input placeholder={t('variantAttributes.valuePlaceholder')} />
          </Form.Item>
          <Form.Item name="displayOrder" label={t('common.displayOrder')} rules={[{ required: true }]}>
            <InputNumber min={0} style={{ width: '100%' }} />
          </Form.Item>
        </Form>
      </FormDialog>
    </div>
  );
}
