import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import {
  Alert,
  App as AntApp,
  Button,
  Descriptions,
  Drawer,
  Input,
  InputNumber,
  Select,
  Space,
  Table,
  Tag,
  Tooltip,
  Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { CheckCircleOutlined, WarningOutlined, WhatsAppOutlined } from '@ant-design/icons';
import type {
  CustomerIdentityTier,
  OrderDetail,
  OrderListItem,
  OrderStatus,
} from '@gentlestore/shared';
import { whatsappLink } from '@gentlestore/shared';
import { api } from '../../api';
import EntityCard from '../../components/EntityCard';
import PageHeader from '../../components/PageHeader';
import ResponsiveTable from '../../components/ResponsiveTable';
import { useResponsive } from '../../hooks/useResponsive';
import { formatDateTime } from '../../lib/format';

const STATUS_COLORS: Record<OrderStatus, string | undefined> = {
  New: 'blue',
  AwaitingQuote: 'gold',
  Quoted: 'cyan',
  Confirmed: 'green',
  Ready: 'lime',
  Completed: undefined,
  Cancelled: 'red',
};

/** Mirrors the server's transition table so the UI only offers moves that will be accepted. */
const NEXT_STATUSES: Record<OrderStatus, OrderStatus[]> = {
  New: ['Confirmed', 'Cancelled'],
  AwaitingQuote: ['Cancelled'],
  Quoted: ['Confirmed', 'Cancelled'],
  Confirmed: ['Ready', 'Completed', 'Cancelled'],
  Ready: ['Completed', 'Cancelled'],
  Completed: [],
  Cancelled: [],
};

const ALL_STATUSES: OrderStatus[] = [
  'New',
  'AwaitingQuote',
  'Quoted',
  'Confirmed',
  'Ready',
  'Completed',
  'Cancelled',
];

function money(amount: number | null, currency: string, fallback: string): string {
  if (amount === null) return fallback;
  try {
    return new Intl.NumberFormat(undefined, { style: 'currency', currency }).format(amount);
  } catch {
    return `${currency} ${amount.toFixed(2)}`;
  }
}

export default function OrdersPage() {
  const { t, i18n } = useTranslation();
  const qc = useQueryClient();
  const { message } = AntApp.useApp();
  const { isMobile } = useResponsive();

  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState<OrderStatus | undefined>();
  const [openId, setOpenId] = useState<string | null>(null);
  /** Prices being typed into the quote editor, keyed by line id. */
  const [quote, setQuote] = useState<Record<string, number | null>>({});
  const [storeNote, setStoreNote] = useState('');

  const listQuery = useQuery({
    queryKey: ['backoffice', 'orders', search, statusFilter ?? 'all'],
    queryFn: () => api.backoffice.listOrders({ search: search || undefined, status: statusFilter }),
  });

  const detailQuery = useQuery({
    queryKey: ['backoffice', 'orders', 'detail', openId],
    queryFn: () => api.backoffice.getOrder(openId!),
    enabled: openId !== null,
  });

  const order = detailQuery.data;

  // Reset the editor whenever a different order is opened.
  useEffect(() => {
    if (!order) return;
    setQuote(Object.fromEntries(order.lines.map((l) => [l.id, l.unitPrice])));
    setStoreNote(order.storeNote ?? '');
  }, [order]);

  const invalidate = () => {
    qc.invalidateQueries({ queryKey: ['backoffice', 'orders'] });
    qc.invalidateQueries({ queryKey: ['backoffice', 'customers'] });
  };

  const setStatus = useMutation({
    mutationFn: (vars: { id: string; status: OrderStatus }) =>
      api.backoffice.updateOrderStatus(vars.id, { status: vars.status, storeNote }),
    onSuccess: () => {
      message.success(t('orders.statusUpdated'));
      invalidate();
    },
    onError: () => message.error(t('orders.statusError')),
  });

  const sendQuote = useMutation({
    mutationFn: (id: string) =>
      api.backoffice.quoteOrder(id, {
        lines: Object.entries(quote)
          .filter(([, price]) => price !== null)
          .map(([lineId, price]) => ({ lineId, unitPrice: price as number })),
        storeNote,
      }),
    onSuccess: () => {
      message.success(t('orders.quoteSent'));
      invalidate();
    },
    onError: () => message.error(t('orders.quoteError')),
  });

  const statusTag = (status: OrderStatus) => (
    <Tag color={STATUS_COLORS[status]} style={{ marginInlineEnd: 0 }}>
      {t(`orderStatus.${status}`)}
    </Tag>
  );

  /**
   * The one thing staff need at a glance: an Invited order's phone was verified by the invite the
   * store itself sent, so it can be acted on. A self-declared one should be confirmed by chat.
   */
  const tierTag = (tier: CustomerIdentityTier) =>
    tier === 'Invited' ? (
      <Tooltip title={t('orders.tierInvitedHint')}>
        <Tag color="green" icon={<CheckCircleOutlined />} style={{ marginInlineEnd: 0 }}>
          {t('orders.tier.Invited')}
        </Tag>
      </Tooltip>
    ) : (
      <Tooltip title={t('orders.tierSelfDeclaredHint')}>
        <Tag color={tier === 'Returning' ? 'blue' : 'orange'} icon={<WarningOutlined />} style={{ marginInlineEnd: 0 }}>
          {t(`orders.tier.${tier}`)}
        </Tag>
      </Tooltip>
    );

  const columns: ColumnsType<OrderListItem> = [
    {
      title: t('orders.colOrder'),
      key: 'order',
      render: (_, r) => (
        <div>
          <div style={{ fontWeight: 600 }}>{r.orderNumber}</div>
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            {formatDateTime(r.placedAt, i18n.language)}
          </Typography.Text>
        </div>
      ),
    },
    {
      title: t('orders.colCustomer'),
      key: 'customer',
      render: (_, r) => (
        <div>
          <Space size={6}>
            <span>{r.contactName}</span>
            {tierTag(r.identityTier)}
          </Space>
          <div>
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              {r.contactPhone}
              {r.customerOrderCount > 1 && ` · ${t('orders.repeatCustomer', { count: r.customerOrderCount })}`}
            </Typography.Text>
          </div>
        </div>
      ),
    },
    { title: t('common.status'), key: 'status', render: (_, r) => statusTag(r.status) },
    {
      title: t('orders.colTotal'),
      key: 'total',
      align: 'end',
      render: (_, r) => money(r.total, r.currency, t('orders.toQuote')),
    },
    {
      title: t('common.actions'),
      key: 'actions',
      align: 'end',
      fixed: 'right',
      render: (_, r) => (
        <Button size="small" type="primary" onClick={() => setOpenId(r.id)}>
          {t('orders.open')}
        </Button>
      ),
    },
  ];

  const needsQuote = order?.status === 'AwaitingQuote' || order?.status === 'Quoted';
  const quoteComplete = order?.lines.every((l) => quote[l.id] !== null && quote[l.id] !== undefined) ?? false;

  return (
    <div>
      <PageHeader
        title={t('orders.title')}
        subtitle={t('orders.subtitle')}
        actions={
          <>
            <Input.Search
              allowClear
              placeholder={t('orders.searchPlaceholder')}
              onSearch={setSearch}
              style={{ width: isMobile ? '100%' : 260 }}
            />
            <Select<OrderStatus | 'all'>
              value={statusFilter ?? 'all'}
              onChange={(value) => setStatusFilter(value === 'all' ? undefined : value)}
              style={{ width: isMobile ? '100%' : 180 }}
              options={[
                { value: 'all', label: t('orders.allStatuses') },
                ...ALL_STATUSES.map((s) => ({ value: s, label: t(`orderStatus.${s}`) })),
              ]}
            />
          </>
        }
      />

      <ResponsiveTable<OrderListItem>
        rowKey="id"
        loading={listQuery.isLoading}
        dataSource={listQuery.data}
        columns={columns}
        pagination={false}
        emptyText={t('orders.empty')}
        cardKey={(r) => r.id}
        renderCard={(r) => (
          <EntityCard
            title={r.orderNumber}
            subtitle={formatDateTime(r.placedAt, i18n.language)}
            extra={statusTag(r.status)}
            fields={[
              { label: t('orders.colCustomer'), value: r.contactName },
              { label: t('orders.identity'), value: tierTag(r.identityTier) },
              { label: t('orders.colTotal'), value: money(r.total, r.currency, t('orders.toQuote')) },
            ]}
            actions={
              <Button type="primary" block onClick={() => setOpenId(r.id)}>
                {t('orders.open')}
              </Button>
            }
          />
        )}
      />

      <Drawer
        open={openId !== null}
        onClose={() => setOpenId(null)}
        placement={isMobile ? 'bottom' : 'right'}
        size={isMobile ? '92dvh' : 560}
        title={order?.orderNumber ?? t('orders.open')}
        loading={detailQuery.isLoading}
      >
        {order && (
          <Space direction="vertical" size={20} style={{ width: '100%' }}>
            {order.identityTier !== 'Invited' && (
              <Alert
                type="warning"
                showIcon
                message={t('orders.unverifiedTitle')}
                description={t('orders.unverifiedBody')}
              />
            )}

            <Space wrap>
              {statusTag(order.status)}
              {tierTag(order.identityTier)}
              <Button
                icon={<WhatsAppOutlined />}
                href={whatsappLink(
                  order.contactPhoneNormalized,
                  t('orders.whatsappMessage', { number: order.orderNumber }),
                )}
                target="_blank"
                rel="noreferrer"
              >
                {t('orders.messageCustomer')}
              </Button>
            </Space>

            <Descriptions
              column={1}
              size="small"
              items={[
                { key: 'name', label: t('orders.customerName'), children: order.contactName },
                { key: 'phone', label: t('common.phoneWhatsapp'), children: order.contactPhone },
                {
                  key: 'origin',
                  label: t('orders.customerRecord'),
                  children: t(`orders.origin.${order.customerOrigin}`),
                },
                {
                  key: 'history',
                  label: t('orders.customerHistory'),
                  children: t('orders.orderCount', { count: order.customerOrderCount }),
                },
                {
                  key: 'fulfilment',
                  label: t('orders.fulfilment'),
                  children: t(`orders.fulfilmentMethod.${order.fulfilment}`),
                },
                ...(order.deliveryAddress
                  ? [{ key: 'address', label: t('orders.address'), children: order.deliveryAddress }]
                  : []),
                ...(order.customerNote
                  ? [{ key: 'note', label: t('orders.customerNote'), children: order.customerNote }]
                  : []),
                { key: 'placed', label: t('orders.placedAt'), children: formatDateTime(order.placedAt, i18n.language) },
              ]}
            />

            <div>
              <Typography.Title level={5}>{t('orders.items')}</Typography.Title>
              <Table<OrderDetail['lines'][number]>
                rowKey="id"
                size="small"
                pagination={false}
                dataSource={order.lines}
                scroll={{ x: 'max-content' }}
                columns={[
                  {
                    title: t('orders.item'),
                    key: 'item',
                    render: (_, l) => (
                      <div>
                        <div>{l.productName}</div>
                        {l.variantLabel && (
                          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                            {l.variantLabel}
                          </Typography.Text>
                        )}
                      </div>
                    ),
                  },
                  { title: t('orders.qty'), dataIndex: 'quantity', key: 'quantity', width: 60 },
                  {
                    title: t('orders.unitPrice'),
                    key: 'unitPrice',
                    width: 140,
                    render: (_, l) =>
                      needsQuote ? (
                        <InputNumber
                          min={0}
                          step={0.5}
                          value={quote[l.id] ?? null}
                          onChange={(value) => setQuote((prev) => ({ ...prev, [l.id]: value ?? null }))}
                          placeholder={t('orders.setPrice')}
                          style={{ width: '100%' }}
                        />
                      ) : (
                        money(l.unitPrice, order.currency, '—')
                      ),
                  },
                  {
                    title: t('orders.lineTotal'),
                    key: 'lineTotal',
                    align: 'end',
                    render: (_, l) => money(l.lineTotal, order.currency, '—'),
                  },
                ]}
                summary={() => (
                  <Table.Summary.Row>
                    <Table.Summary.Cell index={0} colSpan={3}>
                      <strong>{t('orders.colTotal')}</strong>
                    </Table.Summary.Cell>
                    <Table.Summary.Cell index={3} align="end">
                      <strong>{money(order.total, order.currency, t('orders.toQuote'))}</strong>
                    </Table.Summary.Cell>
                  </Table.Summary.Row>
                )}
              />
            </div>

            <div>
              <Typography.Text strong>{t('orders.storeNote')}</Typography.Text>
              <Input.TextArea
                rows={2}
                value={storeNote}
                onChange={(e) => setStoreNote(e.target.value)}
                placeholder={t('orders.storeNotePlaceholder')}
                style={{ marginTop: 6 }}
              />
              <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                {t('orders.storeNoteHint')}
              </Typography.Text>
            </div>

            {needsQuote && (
              <Button
                type="primary"
                block
                disabled={!quoteComplete}
                loading={sendQuote.isPending}
                onClick={() => sendQuote.mutate(order.id)}
              >
                {quoteComplete ? t('orders.sendQuote') : t('orders.priceEveryLine')}
              </Button>
            )}

            {NEXT_STATUSES[order.status].length > 0 ? (
              <Space wrap>
                {NEXT_STATUSES[order.status].map((next) => (
                  <Button
                    key={next}
                    type={next === 'Cancelled' ? 'default' : 'primary'}
                    danger={next === 'Cancelled'}
                    loading={setStatus.isPending && setStatus.variables?.status === next}
                    onClick={() => setStatus.mutate({ id: order.id, status: next })}
                  >
                    {t(`orders.markAs.${next}`)}
                  </Button>
                ))}
              </Space>
            ) : (
              <Typography.Text type="secondary">{t('orders.noFurtherActions')}</Typography.Text>
            )}
          </Space>
        )}
      </Drawer>
    </div>
  );
}
