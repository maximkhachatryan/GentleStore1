import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';

interface Props {
  active?: boolean;
  to: string;
  children: ReactNode;
}

export default function Chip({ active, to, children }: Props) {
  return (
    <Link
      to={to}
      aria-current={active ? 'page' : undefined}
      className={`whitespace-nowrap rounded-full px-4 py-1.5 text-sm font-medium transition ${
        active
          ? 'bg-emerald-600 text-white shadow'
          : 'border border-slate-200 bg-white text-slate-600 hover:border-emerald-300 hover:text-emerald-700'
      }`}
    >
      {children}
    </Link>
  );
}
