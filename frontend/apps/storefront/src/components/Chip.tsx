import type { ReactNode } from 'react';

interface Props {
  active?: boolean;
  onClick?: () => void;
  children: ReactNode;
}

export default function Chip({ active, onClick, children }: Props) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={`whitespace-nowrap rounded-full px-4 py-1.5 text-sm font-medium transition ${
        active
          ? 'bg-emerald-600 text-white shadow'
          : 'border border-slate-200 bg-white text-slate-600 hover:border-emerald-300 hover:text-emerald-700'
      }`}
    >
      {children}
    </button>
  );
}
