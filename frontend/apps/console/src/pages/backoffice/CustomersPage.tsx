import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import {
  Alert,
  App as AntApp,
  Button,
  Descriptions,
  Drawer,
  Dropdown,
  Empty,
  Form,
  Input,
  List,
  Popconfirm,
  Select,
  Space,
  Tag,
  Typography,
} from 'antd';
import type { MenuProps } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { CopyOutlined, MoreOutlined, PlusOutlined, WhatsAppOutlined } from '@ant-design/icons';
import type { Customer, CustomerInviteLink, CustomerStatus } from '@gentlestore/shared';
import { whatsappLink } from '@gentlestore/shared';
import { api } from '../../api';
import EntityCard from '../../components/EntityCard';
import FormDialog from '../../components/FormDialog';
import PageHeader from '../../components/PageHeader';
import ResponsiveTable from '../../components/ResponsiveTable';
import { useResponsive } from '../../hooks/useResponsive';
import { apiErrorCode } from '../../lib/errors';
import { formatDate, formatDateTime } from '../../lib/format';

interface CustomerValues {
  phone: string;
  fullName?: string;
  note?: string;
}

const STATUS_COLORS: Record<CustomerStatus, string | undefined> = {
  new: undefined,
  invited: 'blue',
  active: 'green',
  expired: 'orange',
  blocked: 'red',
};

export default function CustomersPage() {
  const { t, i18n } = useTranslation();
  const qc = useQueryClient();
  const { message, modal } = AntApp.useApp();
  const { isMobile } = useResponsive();

  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState<CustomerStatus | undefined>();
  const [editorOpen, setEditorOpen] = useState(false);
  const [editing, setEditing] = useState<Customer | null>(null);
  const [form] = Form.useForm<CustomerValues>();

  /** The freshly minted link plus who it is for — held only until the dialog closes. */
  const [invite, setInvite] = useState<{ customer: Customer; link: CustomerInviteLink } | null>(null);
  const [detailId, setDetailId] = useState<string | null>(null);

  const storeQuery = useQuery({ queryKey: ['backoffice', 'store'], queryFn: () => api.backoffice.getStore() });

  const listQuery = useQuery({
    queryKey: ['backoffice', 'customers', search, statusFilter ?? 'all'],
    queryFn: () => api.backoffice.listCustomers({ search: search || undefined, status: statusFilter }),
  });

  const detailQuery = useQuery({
    queryKey: ['backoffice', 'customers', detailId],
    queryFn: () => api.backoffice.getCustomer(detailId!),
    enabled: detailId !== null,
  });

  const invalidate = () => qc.invalidateQueries({ queryKey: ['backoffice', 'customers'] });

  const save = useMutation({
    mutationFn: (values: CustomerValues) =>
      editing
        ? api.backoffice.updateCustomer(editing.id, values)
        : api.backoffice.createCustomer(values),
    onSuccess: () => {
      message.success(t('customers.saved'));
      setEditorOpen(false);
      invalidate();
    },
    onError: (error) => {
      const code = apiErrorCode(error);
      message.error(
        code === 'phone_taken'
          ? t('customers.phoneTaken')
          : code === 'phone_missing_country_code'
            ? t('customers.phoneCountryCodeRequired')
            : code === 'phone_invalid'
              ? t('customers.phoneInvalid')
              : t('customers.saveError'),
      );
    },
  });

  const createInvite = useMutation({
    mutationFn: (customer: Customer) =>
      api.backoffice.createCustomerInvite(customer.id).then((link) => ({ customer, link })),
    onSuccess: (result) => {
      setInvite(result);
      invalidate();
    },
    onError: () => message.error(t('customers.inviteError')),
  });

  const revokeInvites = useMutation({
    mutationFn: (id: string) => api.backoffice.revokeCustomerInvites(id),
    onSuccess: () => {
      message.success(t('customers.inviteRevoked'));
      invalidate();
    },
    onError: () => message.error(t('customers.actionError')),
  });

  const revokeDevice = useMutation({
    mutationFn: (vars: { id: string; deviceId: string }) =>
      api.backoffice.revokeCustomerDevice(vars.id, vars.deviceId),
    onSuccess: () => {
      message.success(t('customers.deviceRevoked'));
      invalidate();
    },
    onError: () => message.error(t('customers.actionError')),
  });

  const toggleBlock = useMutation({
    mutationFn: (customer: Customer) =>
      customer.isBlocked ? api.backoffice.unblockCustomer(customer.id) : api.backoffice.blockCustomer(customer.id),
    onSuccess: (_, customer) => {
      message.success(customer.isBlocked ? t('customers.unblocked') : t('customers.blocked'));
      invalidate();
    },
    onError: () => message.error(t('customers.actionError')),
  });

  const remove = useMutation({
    mutationFn: (id: string) => api.backoffice.deleteCustomer(id),
    onSuccess: () => {
      message.success(t('customers.deleted'));
      setDetailId(null);
      invalidate();
    },
    onError: () => message.error(t('customers.actionError')),
  });

  const openCreate = () => {
    setEditing(null);
    form.resetFields();
    setEditorOpen(true);
  };

  const openEdit = (customer: Customer) => {
    setEditing(customer);
    form.setFieldsValue({
      phone: customer.phone,
      fullName: customer.fullName ?? '',
      note: customer.note ?? '',
    });
    setEditorOpen(true);
  };

  const confirmDelete = (customer: Customer) =>
    modal.confirm({
      title: t('customers.deleteConfirm'),
      content: t('customers.deleteConfirmBody'),
      okText: t('common.delete'),
      okButtonProps: { danger: true },
      cancelText: t('common.cancel'),
      onOk: () => remove.mutate(customer.id),
    });

  /**
   * The message that lands in the WhatsApp chat box. It carries the link plus the one rule the
   * customer needs to know: whichever device opens it first is the device that keeps access.
   */
  const inviteMessage = (customer: Customer, url: string) =>
    customer.fullName
      ? t('customers.whatsappMessageNamed', {
          name: customer.fullName,
          store: storeQuery.data?.name ?? '',
          url,
        })
      : t('customers.whatsappMessage', { store: storeQuery.data?.name ?? '', url });

  const copyLink = async (url: string) => {
    try {
      await navigator.clipboard.writeText(url);
      message.success(t('customers.linkCopied'));
    } catch {
      message.error(t('customers.linkCopyFailed'));
    }
  };

  const statusTag = (customer: Customer) => (
    <Tag color={STATUS_COLORS[customer.status]} style={{ marginInlineEnd: 0 }}>
      {t(`customers.status.${customer.status}`)}
    </Tag>
  );

  const moreMenu = (customer: Customer): MenuProps => ({
    items: [
      { key: 'detail', label: t('customers.viewDetails') },
      { key: 'edit', label: t('common.edit') },
      ...(customer.status === 'invited'
        ? [{ key: 'revoke-invite', label: t('customers.cancelInvite') }]
        : []),
      {
        key: 'block',
        label: customer.isBlocked ? t('customers.unblock') : t('customers.block'),
        danger: !customer.isBlocked,
      },
      { type: 'divider' },
      { key: 'delete', label: t('common.delete'), danger: true },
    ],
    onClick: ({ key }) => {
      if (key === 'detail') setDetailId(customer.id);
      else if (key === 'edit') openEdit(customer);
      else if (key === 'revoke-invite') revokeInvites.mutate(customer.id);
      else if (key === 'block') toggleBlock.mutate(customer);
      else if (key === 'delete') confirmDelete(customer);
    },
  });

  const inviteButton = (customer: Customer, block = false) => (
    <Button
      type="primary"
      size={block ? 'middle' : 'small'}
      block={block}
      icon={<WhatsAppOutlined />}
      disabled={customer.isBlocked}
      loading={createInvite.isPending && createInvite.variables?.id === customer.id}
      onClick={() => createInvite.mutate(customer)}
    >
      {customer.status === 'new' ? t('customers.sendInvite') : t('customers.newLink')}
    </Button>
  );

  const columns: ColumnsType<Customer> = [
    {
      title: t('customers.colCustomer'),
      key: 'customer',
      render: (_, r) => (
        <div>
          <div style={{ fontWeight: 600 }}>{r.fullName ?? t('customers.unnamed')}</div>
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            {r.phone}
          </Typography.Text>
        </div>
      ),
    },
    { title: t('common.status'), key: 'status', render: (_, r) => statusTag(r) },
    {
      title: t('customers.colDevices'),
      key: 'devices',
      width: 110,
      render: (_, r) => r.activeDeviceCount,
    },
    {
      title: t('customers.colLastSeen'),
      key: 'lastSeen',
      render: (_, r) => formatDateTime(r.lastSeenAt, i18n.language),
    },
    {
      title: t('common.actions'),
      key: 'actions',
      align: 'end',
      fixed: 'right',
      render: (_, r) => (
        <Space>
          {inviteButton(r)}
          <Dropdown menu={moreMenu(r)} trigger={['click']} placement="bottomRight">
            <Button size="small" icon={<MoreOutlined />} aria-label={t('common.more')} />
          </Dropdown>
        </Space>
      ),
    },
  ];

  const detail = detailQuery.data;
  const detailCustomer = detail?.customer;

  const inviteUrl = invite?.link.url ?? '';
  // wa.me opens the chat with this number and drops the message into its text box, ready to send.
  const inviteWaLink = invite
    ? whatsappLink(invite.customer.phoneNormalized, inviteMessage(invite.customer, inviteUrl))
    : '';

  return (
    <div>
      <PageHeader
        title={t('customers.title')}
        subtitle={t('customers.subtitle')}
        actions={
          <>
            <Input.Search
              allowClear
              placeholder={t('customers.searchPlaceholder')}
              onSearch={setSearch}
              style={{ width: isMobile ? '100%' : 260 }}
            />
            <Select<CustomerStatus | 'all'>
              value={statusFilter ?? 'all'}
              onChange={(value) => setStatusFilter(value === 'all' ? undefined : value)}
              style={{ width: isMobile ? '100%' : 160 }}
              options={[
                { value: 'all', label: t('customers.status.all') },
                { value: 'new', label: t('customers.status.new') },
                { value: 'invited', label: t('customers.status.invited') },
                { value: 'active', label: t('customers.status.active') },
                { value: 'expired', label: t('customers.status.expired') },
                { value: 'blocked', label: t('customers.status.blocked') },
              ]}
            />
            <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
              {t('customers.new')}
            </Button>
          </>
        }
      />

      {storeQuery.data?.storefrontAccess === 'Public' && (
        <Alert
          type="info"
          showIcon
          style={{ marginBottom: 16 }}
          message={t('customers.publicStoreWarning')}
          description={
            <>
              {t('customers.publicStoreWarningBody')}{' '}
              <Link to="/store">{t('customers.publicStoreWarningAction')}</Link>
            </>
          }
        />
      )}

      <ResponsiveTable<Customer>
        rowKey="id"
        loading={listQuery.isLoading}
        dataSource={listQuery.data}
        columns={columns}
        pagination={false}
        emptyText={t('customers.empty')}
        cardKey={(r) => r.id}
        renderCard={(r) => (
          <EntityCard
            title={r.fullName ?? t('customers.unnamed')}
            subtitle={r.phone}
            extra={
              <Space size={4}>
                {statusTag(r)}
                <Dropdown menu={moreMenu(r)} trigger={['click']} placement="bottomRight">
                  <Button type="text" icon={<MoreOutlined />} aria-label={t('common.more')} />
                </Dropdown>
              </Space>
            }
            fields={[
              { label: t('customers.colDevices'), value: r.activeDeviceCount },
              { label: t('customers.colLastSeen'), value: formatDateTime(r.lastSeenAt, i18n.language) },
            ]}
            actions={inviteButton(r, true)}
          />
        )}
      />

      <FormDialog
        title={editing ? t('customers.edit') : t('customers.new')}
        open={editorOpen}
        onCancel={() => setEditorOpen(false)}
        onOk={() => form.submit()}
        confirmLoading={save.isPending}
      >
        <Form form={form} layout="vertical" onFinish={(values) => save.mutate(values)}>
          <Form.Item
            name="phone"
            label={t('common.phoneWhatsapp')}
            extra={t('customers.phoneHint')}
            rules={[{ required: true, message: t('common.phoneRequired') }]}
          >
            <Input placeholder="+374 99 12 34 56" inputMode="tel" />
          </Form.Item>
          <Form.Item name="fullName" label={t('customers.fullName')}>
            <Input placeholder={t('customers.fullNamePlaceholder')} />
          </Form.Item>
          <Form.Item name="note" label={t('customers.note')} extra={t('customers.noteHint')}>
            <Input.TextArea rows={3} />
          </Form.Item>
        </Form>
      </FormDialog>

      {/* The link is displayed once — the server keeps only its hash — so sending it is the
          only thing this dialog is for. */}
      <FormDialog
        title={t('customers.inviteReady')}
        open={invite !== null}
        onCancel={() => setInvite(null)}
        onOk={() => setInvite(null)}
        okText={t('common.done')}
        width={520}
      >
        {invite && (
          <Space direction="vertical" size={16} style={{ width: '100%' }}>
            <Alert
              type="warning"
              showIcon
              message={t('customers.inviteOnceWarning')}
              description={t('customers.inviteOnceWarningBody', {
                date: formatDate(invite.link.expiresAt, i18n.language),
              })}
            />

            <div>
              <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                {t('customers.inviteFor', {
                  name: invite.customer.fullName ?? invite.customer.phone,
                })}
              </Typography.Text>
              <Input.TextArea value={inviteUrl} readOnly autoSize style={{ marginTop: 6 }} />
            </div>

            <Space direction="vertical" size={8} style={{ width: '100%' }}>
              <Button
                type="primary"
                block
                autoFocus
                icon={<WhatsAppOutlined />}
                href={inviteWaLink}
                target="_blank"
                rel="noreferrer"
              >
                {t('customers.openWhatsapp')}
              </Button>
              <Button block icon={<CopyOutlined />} onClick={() => copyLink(inviteUrl)}>
                {t('customers.copyLink')}
              </Button>
            </Space>

            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              {t('customers.openWhatsappHint')}
            </Typography.Text>
          </Space>
        )}
      </FormDialog>

      <Drawer
        open={detailId !== null}
        onClose={() => setDetailId(null)}
        placement={isMobile ? 'bottom' : 'right'}
        size={isMobile ? '92dvh' : 460}
        title={detailCustomer?.fullName ?? detailCustomer?.phone ?? t('customers.viewDetails')}
        loading={detailQuery.isLoading}
      >
        {detail && detailCustomer && (
          <Space direction="vertical" size={20} style={{ width: '100%' }}>
            <Descriptions column={1} size="small" items={[
              { key: 'phone', label: t('common.phoneWhatsapp'), children: detailCustomer.phone },
              { key: 'status', label: t('common.status'), children: statusTag(detailCustomer) },
              {
                key: 'activated',
                label: t('customers.firstActivated'),
                children: formatDateTime(detailCustomer.firstActivatedAt, i18n.language),
              },
              {
                key: 'added',
                label: t('customers.addedOn'),
                children: formatDateTime(detailCustomer.createdAt, i18n.language),
              },
              ...(detailCustomer.note
                ? [{ key: 'note', label: t('customers.note'), children: detailCustomer.note }]
                : []),
            ]} />

            <div>
              <Typography.Title level={5} style={{ marginBottom: 4 }}>
                {t('customers.devices')}
              </Typography.Title>
              <Typography.Paragraph type="secondary" style={{ fontSize: 12 }}>
                {t('customers.devicesHint')}
              </Typography.Paragraph>
              {detail.devices.length === 0 ? (
                <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('customers.noDevices')} />
              ) : (
                <List
                  size="small"
                  dataSource={detail.devices}
                  renderItem={(device) => (
                    <List.Item
                      actions={[
                        <Popconfirm
                          key="revoke"
                          title={t('customers.revokeDeviceConfirm')}
                          onConfirm={() => revokeDevice.mutate({ id: detailCustomer.id, deviceId: device.id })}
                        >
                          <Button size="small" danger>
                            {t('customers.signOutDevice')}
                          </Button>
                        </Popconfirm>,
                      ]}
                    >
                      <List.Item.Meta
                        title={
                          <span style={{ fontSize: 13 }}>
                            {t('customers.lastSeenOn', {
                              when: formatDateTime(device.lastSeenAt, i18n.language),
                            })}
                          </span>
                        }
                        description={
                          <Typography.Text type="secondary" style={{ fontSize: 11, wordBreak: 'break-word' }}>
                            {device.userAgent ?? t('customers.unknownDevice')}
                          </Typography.Text>
                        }
                      />
                    </List.Item>
                  )}
                />
              )}
            </div>

            <div>
              <Typography.Title level={5} style={{ marginBottom: 8 }}>
                {t('customers.inviteHistory')}
              </Typography.Title>
              {detail.invites.length === 0 ? (
                <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('customers.noInvites')} />
              ) : (
                <List
                  size="small"
                  dataSource={detail.invites}
                  renderItem={(item) => (
                    <List.Item>
                      <Space size={8} wrap>
                        <Tag
                          color={
                            item.status === 'used' ? 'green' : item.status === 'pending' ? 'blue' : undefined
                          }
                          style={{ marginInlineEnd: 0 }}
                        >
                          {t(`customers.inviteStatus.${item.status}`)}
                        </Tag>
                        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                          {formatDateTime(item.createdAt, i18n.language)}
                        </Typography.Text>
                      </Space>
                    </List.Item>
                  )}
                />
              )}
            </div>

            {inviteButton(detailCustomer, true)}
          </Space>
        )}
      </Drawer>
    </div>
  );
}
