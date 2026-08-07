import type { ReactNode } from 'react';
import { Typography } from 'antd';
import { useResponsive } from '../hooks/useResponsive';

interface Props {
  title: ReactNode;
  subtitle?: ReactNode;
  /** Filters and primary actions. Each child stretches to full width on phones. */
  actions?: ReactNode;
}

export default function PageHeader({ title, subtitle, actions }: Props) {
  const { isMobile } = useResponsive();

  return (
    <div
      style={{
        display: 'flex',
        flexDirection: isMobile ? 'column' : 'row',
        alignItems: isMobile ? 'stretch' : 'center',
        justifyContent: 'space-between',
        gap: 12,
        marginBottom: 16,
      }}
    >
      <div style={{ minWidth: 0 }}>
        <Typography.Title level={3} style={{ margin: 0, fontSize: isMobile ? 20 : 24 }}>
          {title}
        </Typography.Title>
        {subtitle && (
          <Typography.Text type="secondary" style={{ fontSize: 13 }}>
            {subtitle}
          </Typography.Text>
        )}
      </div>
      {actions && (
        <div
          style={
            isMobile
              ? { display: 'grid', gap: 8 }
              : { display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }
          }
        >
          {actions}
        </div>
      )}
    </div>
  );
}
