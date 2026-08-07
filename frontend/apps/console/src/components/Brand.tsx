interface Props {
  /** Hide the wordmark and keep only the logo tile. */
  compact?: boolean;
}

export default function Brand({ compact }: Props) {
  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 10, minWidth: 0 }}>
      <div
        style={{
          width: 32,
          height: 32,
          flexShrink: 0,
          borderRadius: 9,
          background: 'linear-gradient(135deg,#6366f1,#8b5cf6)',
          color: '#fff',
          display: 'grid',
          placeItems: 'center',
          fontWeight: 700,
          fontSize: 15,
        }}
      >
        G
      </div>
      {!compact && (
        <span style={{ fontWeight: 700, fontSize: 16, letterSpacing: -0.2, whiteSpace: 'nowrap' }}>
          GentleStore
        </span>
      )}
    </div>
  );
}
