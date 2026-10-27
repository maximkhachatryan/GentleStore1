import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import { App as AntApp, Avatar, Button, Dropdown, Form, Input, Space, Switch, Tag, Typography } from 'antd';
import type { MenuProps } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { MoreOutlined, PlusOutlined } from '@ant-design/icons';
import type { StoreListItem } from '@gentlestore/shared';
import { resolveImageUrl } from '@gentlestore/shared';
import { api, API_URL } from '../../api';
import { managedStore } from '../../auth/managedStore';
import ImageUpload from '../../components/ImageUpload';
import EntityCard from '../../components/EntityCard';
import FormDialog from '../../components/FormDialog';
import PageHeader from '../../components/PageHeader';
import ResponsiveTable from '../../components/ResponsiveTable';
import { storefrontBaseFor } from '../../hooks/useStorefrontBase';
import { apiErrorCode } from '../../lib/errors';
import StorePaymentsDialog from './StorePaymentsDialog';

// Why the API turned a domain down; each has its own message.
const DOMAIN_ERRORS: Record<string, string> = {
  domain_invalid: 'stores.domainInvalid',
  domain_platform: 'stores.domainPlatform',
  domain_taken: 'stores.domainTaken',
};

interface StoreFormValues {
  name: string;
  slug?: string;
  customDomain?: string;
  phone: string;
  currency: string;
  description?: string;
  isActive: boolean;
}

export default function StoresPage() {
  const { t } = useTranslation();
  const qc = useQueryClient();
  const { message, modal } = AntApp.useApp();
  const navigate = useNavigate();
  const [open, setOpen] = useState(false);
  const [editing, setEditing] = useState<StoreListItem | null>(null);
  const [payingStore, setPayingStore] = useState<StoreListItem | null>(null);
  const [logoUrl, setLogoUrl] = useState<string | undefined>();
  const [form] = Form.useForm<StoreFormValues>();

  const { data: stores, isLoading } = useQuery({ queryKey: ['admin', 'stores'], queryFn: () => api.admin.listStores() });
  const invalidate = () => qc.invalidateQueries({ queryKey: ['admin', 'stores'] });

  const saveMutation = useMutation({
    mutationFn: (values: StoreFormValues) => {
      const payload = {
        name: values.name,
        slug: values.slug,
        // Sent even when blank: on an edit, blank is how a domain is removed.
        customDomain: values.customDomain ?? '',
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
    onError: (error) => {
      const domainError = DOMAIN_ERRORS[apiErrorCode(error) ?? ''];
      if (domainError) form.setFields([{ name: 'customDomain', errors: [t(domainError)] }]);
      else message.error(t('stores.saveError'));
    },
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

  const confirmDelete = (s: StoreListItem) =>
    modal.confirm({
      title: t('stores.deleteConfirm'),
      content: s.name,
      okText: t('common.delete'),
      okButtonProps: { danger: true },
      cancelText: t('common.cancel'),
      onOk: () => deleteMutation.mutate(s.id),
    });

  const openCreate = () => {
    setEditing(null);
    setLogoUrl(undefined);
    form.resetFields();
    form.setFieldsValue({ currency: 'USD', isActive: true });
    setOpen(true);
  };

  const manageStore = (s: StoreListItem) => {
    managedStore.set({ id: s.id, name: s.name, slug: s.slug, customDomain: s.customDomain });
    navigate('/store');
  };

  const openEdit = async (s: StoreListItem) => {
    const detail = await api.admin.getStore(s.id);
    setEditing(s);
    setLogoUrl(detail.logoUrl ?? undefined);
    form.setFieldsValue({
      name: detail.name,
      slug: detail.slug,
      customDomain: detail.customDomain ?? '',
      phone: detail.phone,
      currency: detail.currency,
      description: detail.description ?? '',
      isActive: detail.isActive,
    });
    setOpen(true);
  };

  const moreMenu = (s: StoreListItem): MenuProps => ({
    items: [
      {
        key: 'view',
        label: (
          <a href={storefrontBaseFor(s)} target="_blank" rel="noreferrer">
            {t('stores.view')}
          </a>
        ),
      },
      { key: 'edit', label: t('common.edit') },
      { key: 'payments', label: t('stores.payments') },
      { key: 'toggle', label: s.isActive ? t('stores.deactivate') : t('stores.activate') },
      { type: 'divider' },
      { key: 'delete', label: t('common.delete'), danger: true },
    ],
    onClick: ({ key }) => {
      if (key === 'edit') openEdit(s);
      else if (key === 'payments') setPayingStore(s);
      else if (key === 'toggle') toggleActive.mutate(s);
      else if (key === 'delete') confirmDelete(s);
    },
  });

  const storeAddress = (s: StoreListItem) => s.customDomain ?? `/${s.slug}`;

  const statusTag = (s: StoreListItem) =>
    s.isActive ? <Tag color="green">{t('common.active')}</Tag> : <Tag>{t('common.inactive')}</Tag>;

  // Only shown when on: a badge on every store that does not take payments would be noise.
  const paymentsTag = (s: StoreListItem) =>
    s.paymentsEnabled ? <Tag color="blue">{t('stores.paymentsOn')}</Tag> : null;

  const columns: ColumnsType<StoreListItem> = [
    {
      title: t('stores.colStore'),
      key: 'store',
      render: (_, r) => (
        <Space>
          <Avatar src={resolveImageUrl(API_URL, r.logoUrl, 'sm')} shape="square">
            {r.name.charAt(0)}
          </Avatar>
          <div>
            <div style={{ fontWeight: 600 }}>{r.name}</div>
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              {storeAddress(r)}
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
      render: (_, r) => (
        <Space size={4} wrap>
          {statusTag(r)}
          {paymentsTag(r)}
        </Space>
      ),
    },
    {
      title: t('common.actions'),
      key: 'actions',
      align: 'end',
      fixed: 'right',
      render: (_, r) => (
        <Space>
          <Button type="primary" size="small" onClick={() => manageStore(r)}>
            {t('stores.manage')}
          </Button>
          <Dropdown menu={moreMenu(r)} trigger={['click']} placement="bottomRight">
            <Button size="small" icon={<MoreOutlined />} aria-label={t('common.more')} />
          </Dropdown>
        </Space>
      ),
    },
  ];

  return (
    <div>
      <PageHeader
        title={t('stores.title')}
        actions={
          <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
            {t('stores.new')}
          </Button>
        }
      />

      <ResponsiveTable<StoreListItem>
        rowKey="id"
        loading={isLoading}
        dataSource={stores}
        columns={columns}
        pagination={false}
        cardKey={(r) => r.id}
        renderCard={(r) => (
          <EntityCard
            media={
              <Avatar size={44} src={resolveImageUrl(API_URL, r.logoUrl, 'sm')} shape="square">
                {r.name.charAt(0)}
              </Avatar>
            }
            title={r.name}
            subtitle={storeAddress(r)}
            extra={
              <Dropdown menu={moreMenu(r)} trigger={['click']} placement="bottomRight">
                <Button type="text" icon={<MoreOutlined />} aria-label={t('common.more')} />
              </Dropdown>
            }
            fields={[
              { label: t('stores.colPhone'), value: r.phone },
              { label: t('stores.colProducts'), value: r.productCount },
              {
                label: t('common.status'),
                value: (
                  <Space size={4} wrap>
                    {statusTag(r)}
                    {paymentsTag(r)}
                  </Space>
                ),
              },
            ]}
            actions={
              <Button type="primary" block onClick={() => manageStore(r)}>
                {t('stores.manage')}
              </Button>
            }
          />
        )}
      />

      <StorePaymentsDialog store={payingStore} onClose={() => setPayingStore(null)} />

      <FormDialog
        title={editing ? t('stores.edit') : t('stores.new')}
        open={open}
        onCancel={() => setOpen(false)}
        onOk={() => form.submit()}
        confirmLoading={saveMutation.isPending}
      >
        <Form form={form} layout="vertical" onFinish={(v) => saveMutation.mutate(v)}>
          <Form.Item name="name" label={t('common.name')} rules={[{ required: true, message: t('common.nameRequired') }]}>
            <Input />
          </Form.Item>
          <Form.Item name="slug" label={t('stores.slug')} tooltip={t('stores.slugTooltip')}>
            <Input placeholder={t('stores.slugPlaceholder')} />
          </Form.Item>
          <Form.Item name="customDomain" label={t('stores.customDomain')} extra={t('stores.customDomainHelp')}>
            <Input placeholder="flowers.am" autoCapitalize="none" spellCheck={false} />
          </Form.Item>
          <Form.Item name="phone" label={t('common.phoneWhatsapp')} rules={[{ required: true, message: t('common.phoneRequired') }]}>
            <Input placeholder="+1 555 123 4567" inputMode="tel" />
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
      </FormDialog>
    </div>
  );
}
