import type { ReactNode } from 'react';
import { Empty, Skeleton, Table } from 'antd';
import type { TableProps } from 'antd';
import { useResponsive } from '../hooks/useResponsive';

interface Props<T> extends TableProps<T> {
  /** Phone rendering for a single record — normally an <EntityCard />. */
  renderCard: (record: T) => ReactNode;
  cardKey: (record: T) => string;
  emptyText?: ReactNode;
}

/**
 * A table on tablets and up, a stack of cards on phones. Horizontal scrolling is
 * confined to the table itself so a wide column set can never widen the page.
 */
export default function ResponsiveTable<T extends object>({
  renderCard,
  cardKey,
  emptyText,
  ...tableProps
}: Props<T>) {
  const { isMobile } = useResponsive();
  const { dataSource, loading } = tableProps;

  if (!isMobile) {
    return <Table<T> {...tableProps} scroll={{ x: 'max-content' }} />;
  }

  if (loading) {
    return <Skeleton active paragraph={{ rows: 4 }} />;
  }

  const records = dataSource ?? [];
  if (records.length === 0) {
    return <Empty description={emptyText} image={Empty.PRESENTED_IMAGE_SIMPLE} />;
  }

  return (
    <div style={{ display: 'grid', gap: 12 }}>
      {records.map((record) => (
        <div key={cardKey(record)}>{renderCard(record)}</div>
      ))}
    </div>
  );
}
