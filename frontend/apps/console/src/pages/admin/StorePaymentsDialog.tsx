import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Alert, App as AntApp, Checkbox, Form, Input, Radio, Skeleton, Space, Switch, Tag, Typography } from 'antd';
import type { PaymentMode, StoreListItem, UpdateStorePaymentsRequest } from '@gentlestore/shared';
import { api } from '../../api';
import FormDialog from '../../components/FormDialog';

interface Props {
  store: StoreListItem | null;
  onClose: () => void;
}

interface FormValues {
  enabled: boolean;
  credentialRef?: string;
  mode: PaymentMode;
  methods: string[];
}

/**
 * Where a super admin switches online payments on for one tenant.
 *
 * The store is pointed at its *own* account with the provider, so customers pay the merchant
 * directly and the platform never holds their money. That is why this asks for a credential
 * reference rather than an API key: the key itself lives in the payment service, and nothing here
 * needs to see it.
 */
export default function StorePaymentsDialog({ store, onClose }: Props) {
  const { t } = useTranslation();
  const { message } = AntApp.useApp();
  const qc = useQueryClient();
  const [form] = Form.useForm<FormValues>();
  const [enabled, setEnabled] = useState(false);

  const { data, isLoading } = useQuery({
    queryKey: ['admin', 'stores', store?.id, 'payments'],
    queryFn: () => api.admin.getStorePayments(store!.id),
    enabled: !!store,
  });

  useEffect(() => {
    if (!data) return;
    setEnabled(data.paymentsEnabled);
    form.setFieldsValue({
      enabled: data.paymentsEnabled,
      credentialRef: data.credentialRef ?? '',
      mode: data.mode,
      methods: data.methods.filter((m) => m.enabled).map((m) => m.key),
    });
  }, [data, form]);

  const save = useMutation({
    mutationFn: (body: UpdateStorePaymentsRequest) => api.admin.updateStorePayments(store!.id, body),
    onSuccess: () => {
      message.success(t('payments.saved'));
      qc.invalidateQueries({ queryKey: ['admin', 'stores'] });
      onClose();
    },
    onError: (error: unknown) => {
      // The API answers with a stable code so the wording stays here rather than on the server.
      const code = (error as { response?: { data?: { code?: string } } })?.response?.data?.code;
      const key = code ? `payments.error_${code}` : '';
      const translated = key && t(key) !== key ? t(key) : t('payments.saveError');
      message.error(translated);
    },
  });

  const submit = (values: FormValues) =>
    save.mutate({
      enabled: values.enabled,
      credentialRef: values.credentialRef?.trim() || null,
      mode: values.mode,
      methods: values.methods ?? [],
    });

  const unavailableText = (problem: string | null) => {
    const key = `payments.unavailable_${problem ?? ''}`;
    return t(key) !== key ? t(key) : t('payments.unavailable_payment_service_unavailable');
  };

  return (
    <FormDialog
      title={t('payments.title')}
      open={!!store}
      onCancel={onClose}
      onOk={() => form.submit()}
      confirmLoading={save.isPending}
      okDisabled={!data?.serviceAvailable}
    >
      {isLoading || !data ? (
        <Skeleton active paragraph={{ rows: 4 }} />
      ) : !data.serviceAvailable ? (
        <Alert
          type="warning"
          showIcon
          message={t('payments.unavailableTitle')}
          description={unavailableText(data.serviceProblem)}
        />
      ) : (
        <Form form={form} layout="vertical" onFinish={submit}>
          <Typography.Paragraph type="secondary" style={{ marginTop: -8 }}>
            {t('payments.subtitle', { store: data.storeName })}
          </Typography.Paragraph>

          <Form.Item name="enabled" label={t('payments.enable')} valuePropName="checked" extra={t('payments.enableHint')}>
            <Switch onChange={setEnabled} />
          </Form.Item>

          {enabled && (
            <>
              <Form.Item
                name="credentialRef"
                label={t('payments.credentialRef')}
                extra={t('payments.credentialRefHint')}
                rules={[{ required: true, message: t('payments.credentialRefRequired') }]}
              >
                <Input placeholder={`${data.storeName.toLowerCase().replace(/\s+/g, '-')}-live`} autoComplete="off" />
              </Form.Item>

              <Form.Item name="mode" label={t('payments.mode')} extra={t('payments.modeHint')}>
                <Radio.Group
                  optionType="button"
                  options={[
                    { label: t('payments.modeTest'), value: 'Test' },
                    { label: t('payments.modeLive'), value: 'Live' },
                  ]}
                />
              </Form.Item>

              <Form.Item
                name="methods"
                label={t('payments.methods')}
                extra={t('payments.methodsHint')}
                rules={[{ required: true, message: t('payments.methodsRequired') }]}
              >
                <Checkbox.Group style={{ width: '100%' }}>
                  <Space direction="vertical" style={{ width: '100%' }}>
                    {data.methods.map((m) => (
                      // A method that cannot take the store's currency is shown but not selectable:
                      // hiding it would leave an admin wondering where the provider went.
                      <Checkbox key={m.key} value={m.key} disabled={!m.currencySupported}>
                        {m.displayName}
                        {!m.currencySupported && (
                          <Tag color="warning" style={{ marginInlineStart: 8 }}>
                            {t('payments.currencyUnsupported', { currency: data.currency })}
                          </Tag>
                        )}
                      </Checkbox>
                    ))}
                  </Space>
                </Checkbox.Group>
              </Form.Item>
            </>
          )}
        </Form>
      )}
    </FormDialog>
  );
}
