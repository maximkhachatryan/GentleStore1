import { useEffect, type CSSProperties } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Alert, App as AntApp, Button, ColorPicker, Form, Input, Radio, Select, Skeleton, Space, Switch, Typography } from 'antd';
import type { Color } from 'antd/es/color-picker';
import {
  contrastRatio,
  loadFonts,
  onBrandColor,
  resolveImageUrl,
  type StoreListItem,
  type StoreThemeRequest,
  type StoreThemeSettings,
} from '@gentlestore/shared';
import { api, API_URL } from '../../api';
import FormDialog from '../../components/FormDialog';
import { storefrontBaseFor } from '../../hooks/useStorefrontBase';

interface Props {
  store: StoreListItem | null;
  onClose: () => void;
}

interface FormValues {
  preset: string;
  brandColor: string | null;
  backgroundColor: string | null;
  headingFont: string | null;
  bodyFont: string | null;
  radius: string | null;
  showStoreName: boolean;
  websiteUrl: string;
}

/** Below this a brand colour used as text (prices, links) on white is hard to read (WCAG, large text). */
const MIN_TEXT_CONTRAST = 3;

/** A picked colour as "#rrggbb"; null once cleared (a cleared picker reports full transparency). */
const pickedHex = (color: Color | null) => {
  const hex = color?.toHexString?.().toLowerCase();
  return hex && /^#[0-9a-f]{6}$/.test(hex) ? hex : null;
};

/**
 * Where a super admin sets a store's storefront look: one of the platform's presets, with any of
 * its values overridden. A field left empty follows the preset, so switching presets later still
 * changes it. Store staff never see this.
 */
export default function StoreThemeDialog({ store, onClose }: Props) {
  const { t } = useTranslation();
  const { message, modal } = AntApp.useApp();
  const qc = useQueryClient();
  const [form] = Form.useForm<FormValues>();
  const queryKey = ['admin', 'stores', store?.id, 'theme'];

  const { data, isLoading } = useQuery({
    queryKey,
    queryFn: () => api.admin.getStoreTheme(store!.id),
    enabled: !!store,
  });

  useEffect(() => {
    if (!data) return;
    const saved = data.theme;
    const preset = data.catalog.presets.find((p) => p.key === (saved?.preset ?? 'classic')) ?? data.catalog.presets[0];
    form.setFieldsValue({
      preset: preset.key,
      brandColor: saved?.brandColor ?? null,
      backgroundColor: saved?.backgroundColor ?? null,
      headingFont: saved?.headingFont ?? null,
      bodyFont: saved?.bodyFont ?? null,
      radius: saved?.radius ?? null,
      showStoreName: saved?.showStoreName ?? preset.showStoreName,
      websiteUrl: saved?.websiteUrl ?? '',
    });
  }, [data, form]);

  const onSaved = (next: StoreThemeSettings, text: string) => {
    qc.setQueryData(queryKey, next);
    message.success(text);
    onClose();
  };

  const save = useMutation({
    mutationFn: (body: StoreThemeRequest) => api.admin.updateStoreTheme(store!.id, body),
    onSuccess: (next) => onSaved(next, t('theme.saved')),
    onError: (error: unknown) => {
      // The API answers with a stable code so the wording stays here rather than on the server.
      const code = (error as { response?: { data?: { code?: string } } })?.response?.data?.code;
      const key = code ? `theme.error_${code}` : '';
      message.error(key && t(key) !== key ? t(key) : t('theme.saveError'));
    },
  });

  const reset = useMutation({
    mutationFn: () => api.admin.resetStoreTheme(store!.id),
    onSuccess: (next) => onSaved(next, t('theme.resetDone')),
    onError: () => message.error(t('theme.saveError')),
  });

  const confirmReset = () =>
    modal.confirm({ title: t('theme.resetConfirm'), onOk: () => reset.mutateAsync() });

  const submit = (v: FormValues) =>
    save.mutate({
      preset: v.preset,
      brandColor: v.brandColor,
      backgroundColor: v.backgroundColor,
      headingFont: v.headingFont,
      bodyFont: v.bodyFont,
      radius: v.radius,
      showStoreName: v.showStoreName,
      websiteUrl: v.websiteUrl?.trim() || null,
    });

  // What the preview shows: the form as it stands, with the preset filling the gaps.
  const values = Form.useWatch([], form) as Partial<FormValues> | undefined;
  const catalog = data?.catalog;
  const preset = catalog?.presets.find((p) => p.key === values?.preset) ?? catalog?.presets[0];
  const brand = values?.brandColor ?? preset?.brandColor ?? '#059669';
  const headingKey = values?.headingFont ?? preset?.headingFont ?? 'system';
  const bodyKey = values?.bodyFont ?? preset?.bodyFont ?? 'system';
  const fontStack = (key: string) => catalog?.fonts.find((f) => f.key === key)?.stack;

  useEffect(() => {
    loadFonts([headingKey, bodyKey]);
  }, [headingKey, bodyKey]);

  const fontLabel = (key: string | undefined) => {
    if (!key) return '';
    return key === 'system' ? t('theme.font_system') : (catalog?.fonts.find((f) => f.key === key)?.label ?? key);
  };
  const radiusLabel = (key: string | undefined) => (key ? t(`theme.radius_${key}`) : '');
  const presetHint = (value: string) => t('theme.fromPreset', { value });

  const fontOptions = (catalog?.fonts ?? []).map((f) => ({
    value: f.key,
    label: <span style={{ fontFamily: f.stack }}>{fontLabel(f.key)}</span>,
  }));

  return (
    <FormDialog
      title={t('theme.title')}
      open={!!store}
      onCancel={onClose}
      onOk={() => form.submit()}
      confirmLoading={save.isPending}
      okDisabled={!data}
      width={760}
    >
      {isLoading || !data || !catalog || !store ? (
        <Skeleton active paragraph={{ rows: 6 }} />
      ) : (
        <>
          <Typography.Paragraph type="secondary" style={{ marginTop: -8 }}>
            {t('theme.subtitle', { store: store.name })}
          </Typography.Paragraph>

          <div style={{ display: 'flex', flexWrap: 'wrap', gap: 24 }}>
            <Form form={form} layout="vertical" onFinish={submit} style={{ flex: '1 1 300px', minWidth: 0 }}>
              <Form.Item name="preset" label={t('theme.preset')}>
                <Radio.Group optionType="button" style={{ display: 'flex', flexWrap: 'wrap', rowGap: 8 }}>
                  {catalog.presets.map((p) => (
                    <Radio.Button key={p.key} value={p.key}>
                      <Space size={6}>
                        <span
                          aria-hidden
                          style={{ display: 'inline-block', width: 10, height: 10, borderRadius: 999, background: p.brandColor }}
                        />
                        {t(`theme.preset_${p.key}`)}
                      </Space>
                    </Radio.Button>
                  ))}
                </Radio.Group>
              </Form.Item>

              <Space size={24} wrap>
                <Form.Item
                  name="brandColor"
                  label={t('theme.brandColor')}
                  extra={t('theme.brandColorHint')}
                  getValueFromEvent={pickedHex}
                >
                  <ColorPicker allowClear disabledAlpha showText format="hex" />
                </Form.Item>
                <Form.Item name="backgroundColor" label={t('theme.backgroundColor')} getValueFromEvent={pickedHex}>
                  <ColorPicker allowClear disabledAlpha showText format="hex" />
                </Form.Item>
              </Space>

              {contrastRatio(brand, '#ffffff') < MIN_TEXT_CONTRAST && (
                <Alert type="warning" showIcon message={t('theme.lowContrast')} style={{ marginBottom: 16 }} />
              )}

              <Form.Item name="headingFont" label={t('theme.headingFont')}>
                <Select allowClear options={fontOptions} placeholder={presetHint(fontLabel(preset?.headingFont))} />
              </Form.Item>
              <Form.Item name="bodyFont" label={t('theme.bodyFont')}>
                <Select allowClear options={fontOptions} placeholder={presetHint(fontLabel(preset?.bodyFont))} />
              </Form.Item>
              <Form.Item name="radius" label={t('theme.radius')}>
                <Select
                  allowClear
                  options={catalog.radii.map((r) => ({ value: r.key, label: radiusLabel(r.key) }))}
                  placeholder={presetHint(radiusLabel(preset?.radius))}
                />
              </Form.Item>
              <Form.Item
                name="showStoreName"
                label={t('theme.showStoreName')}
                extra={t('theme.showStoreNameHint')}
                valuePropName="checked"
              >
                <Switch />
              </Form.Item>
              <Form.Item
                name="websiteUrl"
                label={t('theme.websiteUrl')}
                extra={t('theme.websiteUrlHint')}
                rules={[{ type: 'url', message: t('theme.error_theme_website_invalid') }]}
              >
                <Input placeholder="https://flowers.am" autoComplete="off" />
              </Form.Item>
            </Form>

            <div style={{ flex: '1 1 280px', minWidth: 0 }}>
              <Typography.Text strong>{t('theme.preview')}</Typography.Text>
              <ThemePreview
                storeName={store.name}
                logo={resolveImageUrl(API_URL, store.logoUrl, 'sm')}
                brand={brand}
                background={values?.backgroundColor ?? preset?.backgroundColor ?? '#f8fafc'}
                headingFont={fontStack(headingKey)}
                bodyFont={fontStack(bodyKey)}
                radiusScale={catalog.radii.find((r) => r.key === (values?.radius ?? preset?.radius))?.scale ?? 1}
                showStoreName={values?.showStoreName ?? true}
                websiteUrl={values?.websiteUrl?.trim() || null}
              />
              <Space style={{ marginTop: 12 }} wrap>
                <Button href={storefrontBaseFor(store)} target="_blank" rel="noreferrer">
                  {t('theme.openStorefront')}
                </Button>
                {data.theme && (
                  <Button danger onClick={confirmReset} loading={reset.isPending}>
                    {t('theme.reset')}
                  </Button>
                )}
              </Space>
            </div>
          </div>
        </>
      )}
    </FormDialog>
  );
}

interface PreviewProps {
  storeName: string;
  logo: string | undefined;
  brand: string;
  background: string;
  headingFont: string | undefined;
  bodyFont: string | undefined;
  radiusScale: number;
  showStoreName: boolean;
  websiteUrl: string | null;
}

/**
 * A few storefront pieces drawn with the theme, so the admin sees it before saving. It mirrors the
 * storefront's tokens (apps/storefront/src/index.css) by hand rather than embedding the storefront.
 */
function ThemePreview(p: PreviewProps) {
  const { t } = useTranslation();
  const onBrand = onBrandColor(p.brand);
  const r = (rem: number) => `${rem * p.radiusScale}rem`;
  const brandDark = `color-mix(in oklab, ${p.brand} 85%, black)`;
  const brandTint = `color-mix(in oklab, ${p.brand} 8%, white)`;
  const logoOnly = !!p.logo && !p.showStoreName;
  let websiteHost: string | null = null;
  try {
    websiteHost = p.websiteUrl ? new URL(p.websiteUrl).hostname.replace(/^www\./, '') : null;
  } catch {
    websiteHost = null;
  }

  const card: CSSProperties = {
    background: '#fff',
    border: '1px solid #e2e8f0',
    borderRadius: r(1),
    overflow: 'hidden',
  };

  return (
    <div
      style={{
        marginTop: 8,
        background: p.background,
        fontFamily: p.bodyFont,
        color: '#0f172a',
        border: '1px solid #e2e8f0',
        borderRadius: 8,
        overflow: 'hidden',
      }}
    >
      {websiteHost && (
        <div style={{ background: '#fff', borderBottom: '1px solid #f1f5f9', padding: '2px 12px', fontSize: 11, color: '#64748b' }}>
          ← {websiteHost}
        </div>
      )}
      <div style={{ background: '#fff', borderBottom: '1px solid #e2e8f0', padding: '10px 12px', display: 'flex', alignItems: 'center', gap: 8 }}>
        {logoOnly ? (
          <img src={p.logo} alt="" style={{ height: 28, maxWidth: 140, objectFit: 'contain' }} />
        ) : (
          <>
            <span
              style={{
                width: 28,
                height: 28,
                borderRadius: r(0.5),
                background: p.brand,
                color: onBrand,
                display: 'inline-flex',
                alignItems: 'center',
                justifyContent: 'center',
                overflow: 'hidden',
                fontWeight: 700,
              }}
            >
              {p.logo ? <img src={p.logo} alt="" style={{ width: '100%', height: '100%', objectFit: 'cover' }} /> : p.storeName.charAt(0)}
            </span>
            <span style={{ fontFamily: p.headingFont, fontWeight: 700 }}>{p.storeName}</span>
          </>
        )}
      </div>

      <div style={{ padding: 12 }}>
        <div style={{ display: 'flex', gap: 6, marginBottom: 12 }}>
          <span style={{ background: p.brand, color: onBrand, borderRadius: 999, padding: '2px 10px', fontSize: 12 }}>
            {t('theme.previewAll')}
          </span>
          <span style={{ background: '#fff', border: '1px solid #e2e8f0', color: '#475569', borderRadius: 999, padding: '2px 10px', fontSize: 12 }}>
            {t('theme.previewCategory')}
          </span>
        </div>

        <div style={card}>
          <div style={{ height: 90, background: '#f1f5f9' }} />
          <div style={{ padding: 10 }}>
            <div style={{ fontFamily: p.headingFont, fontWeight: 600 }}>{t('theme.previewProduct')}</div>
            <div style={{ color: brandDark, fontWeight: 600, marginTop: 2 }}>12 500 ֏</div>
            <span style={{ display: 'inline-block', marginTop: 6, background: brandTint, color: brandDark, borderRadius: 999, padding: '1px 8px', fontSize: 11 }}>
              {t('theme.previewCategory')}
            </span>
            <div
              style={{
                marginTop: 10,
                background: p.brand,
                color: onBrand,
                borderRadius: r(0.75),
                padding: '8px 12px',
                textAlign: 'center',
                fontWeight: 600,
                fontSize: 13,
              }}
            >
              {t('theme.previewAdd')}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
