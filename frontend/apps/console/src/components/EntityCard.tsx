import type { ReactNode } from 'react';
import { Card, Typography } from 'antd';

export interface EntityField {
  label: ReactNode;
  value: ReactNode;
}

interface Props {
  media?: ReactNode;
  title: ReactNode;
  subtitle?: ReactNode;
  /** Rendered top-right, next to the title — usually a status tag or overflow menu. */
  extra?: ReactNode;
  fields?: EntityField[];
  /** Free-form block between the fields and the action bar (tags, chips, …). */
  children?: ReactNode;
  actions?: ReactNode;
}

/**
 * The phone-sized counterpart to a table row: one record per card, with a
 * consistent title / detail / action rhythm across every list in the console.
 */
export default function EntityCard({ media, title, subtitle, extra, fields, children, actions }: Props) {
  return (
    <Card size="small" styles={{ body: { padding: 14 } }}>
      <div style={{ display: 'flex', alignItems: 'flex-start', gap: 12 }}>
        {media}
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{ fontWeight: 600, wordBreak: 'break-word' }}>{title}</div>
          {subtitle && (
            <Typography.Text type="secondary" style={{ fontSize: 12, wordBreak: 'break-all' }}>
              {subtitle}
            </Typography.Text>
          )}
        </div>
        {extra && <div style={{ flexShrink: 0 }}>{extra}</div>}
      </div>

      {fields && fields.length > 0 && (
        <div style={{ marginTop: 12, display: 'grid', gap: 6 }}>
          {fields.map((field, index) => (
            <div key={index} style={{ display: 'flex', gap: 12, alignItems: 'baseline', fontSize: 13 }}>
              <Typography.Text type="secondary" style={{ fontSize: 13, flexShrink: 0 }}>
                {field.label}
              </Typography.Text>
              <div style={{ marginInlineStart: 'auto', textAlign: 'end', minWidth: 0 }}>{field.value}</div>
            </div>
          ))}
        </div>
      )}

      {children && <div style={{ marginTop: 12 }}>{children}</div>}

      {actions && (
        <div style={{ marginTop: 14, display: 'flex', gap: 8, flexWrap: 'wrap' }}>{actions}</div>
      )}
    </Card>
  );
}
