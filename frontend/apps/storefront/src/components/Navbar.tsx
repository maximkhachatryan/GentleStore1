import { Link } from 'react-router-dom';
import type { ReactNode } from 'react';
import LanguageSwitcher from './LanguageSwitcher';

export default function Navbar({ children }: { children?: ReactNode }) {
  return (
    <header className="sticky top-0 z-30 border-b border-slate-200 bg-white/80 backdrop-blur">
      <div className="mx-auto flex h-14 max-w-6xl items-center justify-between px-4">
        <Link to="/" className="flex items-center gap-2 text-lg font-bold text-emerald-600">
          <span className="inline-flex h-8 w-8 items-center justify-center rounded-lg bg-emerald-600 text-white">G</span>
          GentleStore
        </Link>
        <div className="flex items-center gap-3">
          {children}
          <LanguageSwitcher />
        </div>
      </div>
    </header>
  );
}
