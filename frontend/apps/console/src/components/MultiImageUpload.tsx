import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { App as AntApp, Button, Upload } from 'antd';
import { UploadOutlined } from '@ant-design/icons';
import { api } from '../api';
import { MAX_BYTES } from './ImageUpload';

interface Props {
  // Receives the uploaded URLs in selection order. Awaited so the button stays busy
  // until the caller has finished attaching them.
  onUploaded: (urls: string[]) => Promise<unknown> | void;
  buttonText?: string;
}

export default function MultiImageUpload({ onUploaded, buttonText }: Props) {
  const { t } = useTranslation();
  const { message } = AntApp.useApp();
  const [uploading, setUploading] = useState(false);

  const uploadAll = async (files: File[]) => {
    const accepted = files.filter((f) => f.size <= MAX_BYTES);
    if (accepted.length < files.length) {
      message.error(t('imageUpload.tooLarge', { mb: MAX_BYTES / 1024 / 1024 }));
    }
    if (!accepted.length) return;

    setUploading(true);
    try {
      const results = await Promise.allSettled(accepted.map((f) => api.uploads.upload(f)));
      const urls = results.flatMap((r) => (r.status === 'fulfilled' ? [r.value.url] : []));
      const failed = results.length - urls.length;
      if (urls.length) await onUploaded(urls);
      if (failed) message.error(t('imageUpload.someFailed', { count: failed }));
      else message.success(t('imageUpload.uploadedMany', { count: urls.length }));
    } catch {
      message.error(t('imageUpload.failed'));
    } finally {
      setUploading(false);
    }
  };

  return (
    <Upload
      accept="image/*"
      multiple
      showUploadList={false}
      beforeUpload={(file, fileList) => {
        // antd calls this once per selected file with the same list, so the batch is
        // handled on the first call and antd's own per-file upload is skipped.
        if (file === fileList[0]) void uploadAll(fileList);
        return Upload.LIST_IGNORE;
      }}
    >
      <Button icon={<UploadOutlined />} loading={uploading}>
        {buttonText ?? t('imageUpload.upload')}
      </Button>
    </Upload>
  );
}
