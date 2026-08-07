import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { BrowserRouter } from 'react-router-dom';
import { App as AntApp, ConfigProvider } from 'antd';
import hyAM from 'antd/locale/hy_AM';
import enUS from 'antd/locale/en_US';
import ruRU from 'antd/locale/ru_RU';
import { useTranslation } from 'react-i18next';
import { AuthProvider } from './auth/AuthContext';
import App from './App';
import { consoleTheme } from './theme';
import './i18n';
import './index.css';

const queryClient = new QueryClient({
  defaultOptions: { queries: { retry: 1, refetchOnWindowFocus: false } },
});

const ANTD_LOCALES = { hy: hyAM, en: enUS, ru: ruRU } as const;

function Root() {
  const { i18n } = useTranslation();
  const locale = ANTD_LOCALES[i18n.language as keyof typeof ANTD_LOCALES] ?? hyAM;

  return (
    <ConfigProvider locale={locale} theme={consoleTheme}>
      <AntApp>
        <QueryClientProvider client={queryClient}>
          <BrowserRouter>
            <AuthProvider>
              <App />
            </AuthProvider>
          </BrowserRouter>
        </QueryClientProvider>
      </AntApp>
    </ConfigProvider>
  );
}

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <Root />
  </StrictMode>,
);
