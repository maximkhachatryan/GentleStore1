import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { App as AntApp, Button, Upload } from 'antd';
import { UploadOutlined } from '@ant-design/icons';
import { resolveAssetUrl } from '@gentlestore/shared';
import { api, API_URL } from '../api';

// Mirrors UploadsController.MaxBytes. Rejecting here keeps an oversized file from
// hitting the reverse proxy, which answers 413 without CORS headers — the browser
// surfaces that as an opaque network error rather than a readable message.
const MAX_BYTES = 5 * 1024 * 1024;

interface Props {
  value?: string | null;
  onChange?: (url: string) => void;
  buttonText?: string;
}

export default function ImageUpload({ value, onChange, buttonText }: Props) {
  const { t } = useTranslation();
  const { message } = AntApp.useApp();
  const [uploading, setUploading] = useState(false);
  const preview = resolveAssetUrl(API_URL, value);

  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
      {preview && (
        <img
          src={preview}
          alt="preview"
          style={{ width: 64, height: 64, objectFit: 'cover', borderRadius: 8, border: '1px solid #f0f0f0' }}
        />
      )}
      <Upload
        accept="image/*"
        showUploadList={false}
        beforeUpload={(file) => {
          if (file.size > MAX_BYTES) {
            message.error(t('imageUpload.tooLarge', { mb: MAX_BYTES / 1024 / 1024 }));
            return Upload.LIST_IGNORE;
          }
          return true;
        }}
        customRequest={async ({ file, onSuccess, onError }) => {
          setUploading(true);
          try {
            const res = await api.uploads.upload(file as File);
            onChange?.(res.url);
            onSuccess?.(res);
            message.success(t('imageUpload.uploaded'));
          } catch (e) {
            onError?.(e as Error);
            message.error(t('imageUpload.failed'));
          } finally {
            setUploading(false);
          }
        }}
      >
        <Button icon={<UploadOutlined />} loading={uploading}>
          {buttonText ?? t('imageUpload.upload')}
        </Button>
      </Upload>
    </div>
  );
}
