import { useTranslation } from 'react-i18next';
import { SUPPORTED_LANGUAGES, LANGUAGE_NAMES, type Language } from '../i18n';

export default function LanguageSwitcher() {
  const { i18n } = useTranslation();

  return (
    <select
      aria-label="Language"
      value={i18n.language}
      onChange={(e) => i18n.changeLanguage(e.target.value)}
      className="rounded-lg border border-slate-200 bg-white px-2 py-1 text-sm font-medium text-slate-700 outline-none focus:border-emerald-400"
    >
      {SUPPORTED_LANGUAGES.map((lng) => (
        <option key={lng} value={lng}>
          {LANGUAGE_NAMES[lng as Language]}
        </option>
      ))}
    </select>
  );
}
