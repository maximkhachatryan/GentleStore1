import { useSyncExternalStore } from 'react';

const STORAGE_KEY = 'gentlestore_managed_store';

export interface ManagedStore {
  id: string;
  name: string;
  slug: string;
}

function read(): ManagedStore | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    return raw ? (JSON.parse(raw) as ManagedStore) : null;
  } catch {
    return null;
  }
}

let cache: ManagedStore | null = read();
const listeners = new Set<() => void>();

function emit(): void {
  listeners.forEach((listener) => listener());
}

export const managedStore = {
  get: (): ManagedStore | null => cache,
  set(store: ManagedStore): void {
    cache = store;
    localStorage.setItem(STORAGE_KEY, JSON.stringify(store));
    emit();
  },
  clear(): void {
    cache = null;
    localStorage.removeItem(STORAGE_KEY);
    emit();
  },
  subscribe(listener: () => void): () => void {
    listeners.add(listener);
    return () => listeners.delete(listener);
  },
};

export function useManagedStore(): ManagedStore | null {
  return useSyncExternalStore(managedStore.subscribe, managedStore.get, () => null);
}
