import type { ThemeConfig } from 'antd';

export const BRAND = '#4f46e5';
export const BRAND_SOFT = '#eef2ff';

export const FONT_FAMILY =
  "-apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif";

export const consoleTheme: ThemeConfig = {
  token: {
    colorPrimary: BRAND,
    colorInfo: BRAND,
    colorLink: BRAND,
    colorBgLayout: '#f4f5fa',
    borderRadius: 10,
    fontFamily: FONT_FAMILY,
  },
  components: {
    Layout: {
      headerBg: '#ffffff',
      siderBg: '#ffffff',
      bodyBg: '#f4f5fa',
      headerPadding: '0 16px',
    },
    Menu: {
      itemHeight: 42,
      itemBorderRadius: 8,
      itemMarginInline: 8,
      itemSelectedBg: BRAND_SOFT,
      itemSelectedColor: BRAND,
    },
    Table: {
      headerBg: '#fafafc',
      headerColor: '#64748b',
      cellPaddingBlock: 14,
    },
    Card: {
      headerFontSize: 15,
    },
  },
};
