import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import hy from './locales/hy.json';
import en from './locales/en.json';
import ru from './locales/ru.json';

export const SUPPORTED_LANGUAGES = ['hy', 'en', 'ru'] as const;
export type Language = (typeof SUPPORTED_LANGUAGES)[number];

export const LANGUAGE_NAMES: Record<Language, string> = {
  hy: 'Հայերեն',
  en: 'English',
  ru: 'Русский',
};

const STORAGE_KEY = 'gentlestore_lang';

function initialLanguage(): Language {
  const saved = localStorage.getItem(STORAGE_KEY);
  if (saved && (SUPPORTED_LANGUAGES as readonly string[]).includes(saved)) {
    return saved as Language;
  }
  return 'hy';
}

i18n.use(initReactI18next).init({
  resources: {
    hy: { translation: hy },
    en: { translation: en },
    ru: { translation: ru },
  },
  lng: initialLanguage(),
  fallbackLng: 'hy',
  interpolation: { escapeValue: false },
});

i18n.on('languageChanged', (lng) => {
  localStorage.setItem(STORAGE_KEY, lng);
  document.documentElement.lang = lng;
});

document.documentElement.lang = i18n.language;

export default i18n;
