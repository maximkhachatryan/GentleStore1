import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { App as AntApp, Button, Upload } from 'antd';
import { UploadOutlined } from '@ant-design/icons';
import { resolveAssetUrl } from '@gentlestore/shared';
import { api, API_URL } from '../api';

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
