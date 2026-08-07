import type { ReactNode } from 'react';
import { Button, Drawer, Modal } from 'antd';
import { useTranslation } from 'react-i18next';
import { useResponsive } from '../hooks/useResponsive';

interface Props {
  title: ReactNode;
  open: boolean;
  onCancel: () => void;
  onOk: () => void;
  okText?: string;
  confirmLoading?: boolean;
  width?: number;
  children: ReactNode;
}

/**
 * Centered dialog on desktop, full-height bottom sheet on phones — long forms stay
 * reachable with one thumb and the submit button never scrolls out of view.
 */
export default function FormDialog({
  title,
  open,
  onCancel,
  onOk,
  okText,
  confirmLoading,
  width = 560,
  children,
}: Props) {
  const { t } = useTranslation();
  const { isMobile } = useResponsive();
  const submitLabel = okText ?? t('common.save');

  if (isMobile) {
    return (
      <Drawer
        title={title}
        open={open}
        onClose={onCancel}
        placement="bottom"
        size="92dvh"
        destroyOnHidden
        styles={{
          body: { paddingBottom: 8 },
          footer: { padding: '12px 16px calc(12px + env(safe-area-inset-bottom))' },
        }}
        footer={
          <div style={{ display: 'flex', gap: 8 }}>
            <Button block onClick={onCancel}>
              {t('common.cancel')}
            </Button>
            <Button block type="primary" loading={confirmLoading} onClick={onOk}>
              {submitLabel}
            </Button>
          </div>
        }
      >
        {children}
      </Drawer>
    );
  }

  return (
    <Modal
      title={title}
      open={open}
      onCancel={onCancel}
      onOk={onOk}
      okText={submitLabel}
      cancelText={t('common.cancel')}
      confirmLoading={confirmLoading}
      width={width}
      destroyOnHidden
      styles={{ body: { maxHeight: 'calc(100vh - 240px)', overflowY: 'auto', paddingBlock: 4 } }}
    >
      {children}
    </Modal>
  );
}
