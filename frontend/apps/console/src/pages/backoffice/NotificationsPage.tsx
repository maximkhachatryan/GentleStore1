import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Alert, App as AntApp, Button, Card, Popconfirm, Space, Switch, Tag, Typography } from 'antd';
import { BellOutlined, SendOutlined } from '@ant-design/icons';
import { api } from '../../api';
import PageHeader from '../../components/PageHeader';
import { alertSound, playChime } from '../../hooks/useOrderAlerts';

const TELEGRAM_KEY = ['backoffice', 'notifications', 'telegram'];

type Permission = NotificationPermission | 'unsupported';

const currentPermission = (): Permission =>
  'Notification' in window ? Notification.permission : 'unsupported';

export default function NotificationsPage() {
  const { t, i18n } = useTranslation();
  const qc = useQueryClient();
  const { message } = AntApp.useApp();

  /** The one-time t.me link, held while we wait for the user to press Start. */
  const [pendingLink, setPendingLink] = useState<string | null>(null);
  const [permission, setPermission] = useState<Permission>(currentPermission);
  const [sound, setSound] = useState(alertSound.enabled);

  const telegram = useQuery({
    queryKey: TELEGRAM_KEY,
    queryFn: () => api.backoffice.getTelegram(),
    // Watch for the bot binding the chat; stop as soon as it has.
    refetchInterval: (q) => (pendingLink && !q.state.data?.linked ? 3000 : false),
  });

  const linked = telegram.data?.linked ?? false;
  useEffect(() => {
    if (!pendingLink || !linked) return;
    setPendingLink(null);
    message.success(t('notifications.linkedSuccess'));
  }, [pendingLink, linked, message, t]);

  const link = useMutation({
    mutationFn: () => api.backoffice.linkTelegram(i18n.language),
    onSuccess: (res) => setPendingLink(res.url),
    onError: () => message.error(t('notifications.linkError')),
  });

  const unlink = useMutation({
    mutationFn: () => api.backoffice.unlinkTelegram(),
    onSuccess: () => {
      message.success(t('notifications.unlinked'));
      qc.invalidateQueries({ queryKey: TELEGRAM_KEY });
    },
  });

  const requestPermission = async () => {
    setPermission(await Notification.requestPermission());
  };

  const toggleSound = (on: boolean) => {
    alertSound.set(on);
    setSound(on);
    if (on) playChime();
  };

  return (
    <div style={{ maxWidth: 640 }}>
      <PageHeader title={t('notifications.title')} subtitle={t('notifications.subtitle')} />

      <Space direction="vertical" size={16} style={{ width: '100%' }}>
        <Card
          loading={telegram.isLoading}
          title={
            <Space>
              <SendOutlined />
              {t('notifications.telegramTitle')}
            </Space>
          }
          extra={
            telegram.data?.available &&
            (linked ? (
              <Tag color="green">{t('notifications.connected')}</Tag>
            ) : (
              <Tag>{t('notifications.notConnected')}</Tag>
            ))
          }
        >
          {telegram.data && !telegram.data.available ? (
            <Alert type="info" showIcon title={t('notifications.telegramUnavailable')} />
          ) : (
            <Space direction="vertical" size={12} style={{ width: '100%' }}>
              <Typography.Text type="secondary">{t('notifications.telegramHint')}</Typography.Text>

              {linked ? (
                <Popconfirm title={t('notifications.disconnectConfirm')} onConfirm={() => unlink.mutate()}>
                  <Button danger loading={unlink.isPending}>
                    {t('notifications.disconnect')}
                  </Button>
                </Popconfirm>
              ) : pendingLink ? (
                <Alert
                  type="info"
                  showIcon
                  title={t('notifications.waitingHint')}
                  action={
                    <Button type="primary" href={pendingLink} target="_blank" rel="noreferrer">
                      {t('notifications.openTelegram')}
                    </Button>
                  }
                />
              ) : (
                <Button type="primary" icon={<SendOutlined />} loading={link.isPending} onClick={() => link.mutate()}>
                  {t('notifications.connect')}
                </Button>
              )}
            </Space>
          )}
        </Card>

        <Card
          title={
            <Space>
              <BellOutlined />
              {t('notifications.desktopTitle')}
            </Space>
          }
        >
          <Space direction="vertical" size={12} style={{ width: '100%' }}>
            <Typography.Text type="secondary">{t('notifications.desktopHint')}</Typography.Text>

            {permission === 'granted' && <Tag color="green">{t('notifications.desktopOn')}</Tag>}
            {permission === 'denied' && <Alert type="warning" showIcon title={t('notifications.desktopDenied')} />}
            {permission === 'unsupported' && <Alert type="info" showIcon title={t('notifications.desktopUnsupported')} />}
            {permission === 'default' && (
              <Button icon={<BellOutlined />} onClick={requestPermission}>
                {t('notifications.desktopEnable')}
              </Button>
            )}

            <Space>
              <Switch checked={sound} onChange={toggleSound} />
              <span>{t('notifications.sound')}</span>
            </Space>
          </Space>
        </Card>
      </Space>
    </div>
  );
}
