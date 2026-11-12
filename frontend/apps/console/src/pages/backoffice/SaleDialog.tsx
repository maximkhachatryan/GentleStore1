import { useMutation } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { App as AntApp, Button, Divider, Form, Input, InputNumber, Popconfirm, Radio, Select, Typography } from 'antd';
import type { SaleScope } from '@gentlestore/shared';
import { api } from '../../api';
import FormDialog from '../../components/FormDialog';
import { endOfDayIso, todayInput } from '../../lib/sale';

type ScopeKind = 'all' | 'category' | 'tag';

interface Values {
  scope: ScopeKind;
  categoryId?: string;
  tagId?: string;
  percent?: number;
  endsDate?: string;
}

interface Option {
  label: string;
  value: string;
}

interface Props {
  open: boolean;
  onClose: () => void;
  categoryOptions: Option[];
  tagOptions: Option[];
  /** Called after products changed, so the list can refresh. */
  onChanged: () => void;
}

const scopeOf = (v: Pick<Values, 'scope' | 'categoryId' | 'tagId'>): SaleScope => ({
  categoryId: v.scope === 'category' ? v.categoryId : null,
  tagId: v.scope === 'tag' ? v.tagId : null,
});

/** Puts a whole category, tag or store on sale at a percentage off — or takes it off sale. */
export default function SaleDialog({ open, onClose, categoryOptions, tagOptions, onChanged }: Props) {
  const { t } = useTranslation();
  const { message } = AntApp.useApp();
  const [form] = Form.useForm<Values>();
  const scope: ScopeKind = Form.useWatch('scope', form) ?? 'all';

  const close = () => {
    form.resetFields();
    onClose();
  };

  const apply = useMutation({
    mutationFn: (v: Values) =>
      api.backoffice.applySale({
        ...scopeOf(v),
        percent: v.percent!,
        endsAt: v.endsDate ? endOfDayIso(v.endsDate) : null,
      }),
    onSuccess: (result) => {
      message.success(t('products.sale.applied', { count: result.productCount }));
      onChanged();
      close();
    },
    onError: () => message.error(t('products.sale.error')),
  });

  const end = useMutation({
    mutationFn: (v: Values) => api.backoffice.endSale(scopeOf(v)),
    onSuccess: (result) => {
      message.success(t('products.sale.ended', { count: result.productCount }));
      onChanged();
      close();
    },
    onError: () => message.error(t('products.sale.error')),
  });

  // Ending a sale only needs to know which products; the discount fields may be blank.
  const endSale = () =>
    form
      .validateFields(['scope', 'categoryId', 'tagId'])
      .then((v) => end.mutate(v as Values))
      .catch(() => undefined);

  return (
    <FormDialog
      title={t('products.sale.title')}
      open={open}
      onCancel={close}
      onOk={() => form.submit()}
      okText={t('products.sale.apply')}
      confirmLoading={apply.isPending}
    >
      <Typography.Paragraph type="secondary">{t('products.sale.hint')}</Typography.Paragraph>
      <Form form={form} layout="vertical" initialValues={{ scope: 'all' }} onFinish={(v) => apply.mutate(v)}>
        <Form.Item name="scope" label={t('products.sale.scope')}>
          <Radio.Group optionType="button" buttonStyle="solid">
            <Radio.Button value="all">{t('products.sale.scopeAll')}</Radio.Button>
            <Radio.Button value="category">{t('products.sale.scopeCategory')}</Radio.Button>
            <Radio.Button value="tag">{t('products.sale.scopeTag')}</Radio.Button>
          </Radio.Group>
        </Form.Item>
        {scope === 'category' && (
          <Form.Item name="categoryId" rules={[{ required: true, message: t('products.sale.categoryRequired') }]}>
            <Select options={categoryOptions} placeholder={t('products.selectCategory')} />
          </Form.Item>
        )}
        {scope === 'tag' && (
          <Form.Item name="tagId" rules={[{ required: true, message: t('products.sale.tagRequired') }]}>
            <Select options={tagOptions} placeholder={t('products.selectTag')} />
          </Form.Item>
        )}
        <Form.Item
          name="percent"
          label={t('products.sale.percent')}
          rules={[{ required: true, type: 'number', min: 1, max: 95, message: t('products.sale.percentRequired') }]}
        >
          <InputNumber min={1} max={95} precision={0} style={{ width: 160 }} />
        </Form.Item>
        <Form.Item name="endsDate" label={t('products.sale.endsAt')} extra={t('products.sale.endsAtHint')}>
          <Input type="date" min={todayInput()} style={{ width: 200 }} />
        </Form.Item>
      </Form>

      <Divider style={{ margin: '8px 0 16px' }} />
      <Popconfirm title={t('products.sale.endConfirm')} onConfirm={endSale}>
        <Button danger loading={end.isPending}>
          {t('products.sale.end')}
        </Button>
      </Popconfirm>
    </FormDialog>
  );
}
