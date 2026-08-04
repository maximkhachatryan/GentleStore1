import { Select } from 'antd';
import { GlobalOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { SUPPORTED_LANGUAGES, LANGUAGE_NAMES, type Language } from '../i18n';

export default function LanguageSwitcher() {
  const { i18n } = useTranslation();

  return (
    <Select
      size="small"
      value={i18n.language}
      onChange={(lng) => i18n.changeLanguage(lng)}
      style={{ width: 130 }}
      suffixIcon={<GlobalOutlined />}
      options={SUPPORTED_LANGUAGES.map((lng) => ({ value: lng, label: LANGUAGE_NAMES[lng as Language] }))}
    />
  );
}
