import { useTranslation } from 'react-i18next';
import { App as AntApp, Button, Tooltip } from 'antd';
import { CopyOutlined, WhatsAppOutlined } from '@ant-design/icons';
import { whatsappShareLink } from '@gentlestore/shared';

interface Props {
  /** Storefront link to hand out. */
  url: string;
  /** Shown above the link in WhatsApp, e.g. the product name. */
  label: string;
}

/** "Send via WhatsApp" lets staff pick the customer in WhatsApp itself; "Copy" is for anywhere else. */
export default function ShareButtons({ url, label }: Props) {
  const { t } = useTranslation();
  const { message } = AntApp.useApp();

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(url);
      message.success(t('share.copied'));
    } catch {
      message.error(t('share.copyError'));
    }
  };

  return (
    <>
      <Tooltip title={t('share.whatsapp')}>
        <Button
          size="small"
          icon={<WhatsAppOutlined />}
          href={whatsappShareLink(`${label}\n${url}`)}
          target="_blank"
          rel="noreferrer"
          aria-label={t('share.whatsapp')}
        />
      </Tooltip>
      <Tooltip title={t('share.copy')}>
        <Button size="small" icon={<CopyOutlined />} onClick={copy} aria-label={t('share.copy')} />
      </Tooltip>
    </>
  );
}
