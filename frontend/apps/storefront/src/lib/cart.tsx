import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import type { ReactNode } from 'react';

export interface CartLine {
  productId: string;
  variantId: string | null;
  /** Snapshots for display only — the server re-reads the real price when the order is placed. */
  name: string;
  variantLabel: string | null;
  unitPrice: number | null;
  imageUrl: string | null;
  quantity: number;
}

interface CartApi {
  lines: CartLine[];
  itemCount: number;
  /** Null when any line is "price on request", so the storefront shows that instead of a wrong total. */
  subtotal: number | null;
  add: (line: Omit<CartLine, 'quantity'>, quantity?: number) => void;
  setQuantity: (productId: string, variantId: string | null, quantity: number) => void;
  remove: (productId: string, variantId: string | null) => void;
  clear: () => void;
}

const MAX_QUANTITY = 99;
const CartContext = createContext<CartApi | null>(null);

/** Carts are per store — a customer may be shopping at several of them in the same browser. */
const storageKey = (slug: string) => `gentlestore_cart_${slug}`;

function read(slug: string): CartLine[] {
  try {
    const raw = localStorage.getItem(storageKey(slug));
    if (!raw) return [];
    const parsed = JSON.parse(raw);
    return Array.isArray(parsed) ? (parsed as CartLine[]) : [];
  } catch {
    // Corrupt or unavailable storage should never take the storefront down with it.
    return [];
  }
}

const same = (line: CartLine, productId: string, variantId: string | null) =>
  line.productId === productId && (line.variantId ?? null) === variantId;

export function CartProvider({ slug, children }: { slug: string; children: ReactNode }) {
  const [lines, setLines] = useState<CartLine[]>(() => read(slug));

  // Re-read when the shopper walks from one store to another.
  useEffect(() => setLines(read(slug)), [slug]);

  useEffect(() => {
    try {
      if (lines.length === 0) localStorage.removeItem(storageKey(slug));
      else localStorage.setItem(storageKey(slug), JSON.stringify(lines));
    } catch {
      // Private-mode quota errors are not worth surfacing; the cart just won't survive a reload.
    }
  }, [slug, lines]);

  const add = useCallback((line: Omit<CartLine, 'quantity'>, quantity = 1) => {
    setLines((current) => {
      const index = current.findIndex((l) => same(l, line.productId, line.variantId ?? null));
      if (index === -1) return [...current, { ...line, quantity: Math.min(quantity, MAX_QUANTITY) }];

      const next = [...current];
      next[index] = {
        ...next[index],
        quantity: Math.min(next[index].quantity + quantity, MAX_QUANTITY),
      };
      return next;
    });
  }, []);

  const setQuantity = useCallback((productId: string, variantId: string | null, quantity: number) => {
    setLines((current) =>
      quantity <= 0
        ? current.filter((l) => !same(l, productId, variantId))
        : current.map((l) =>
            same(l, productId, variantId) ? { ...l, quantity: Math.min(quantity, MAX_QUANTITY) } : l,
          ),
    );
  }, []);

  const remove = useCallback((productId: string, variantId: string | null) => {
    setLines((current) => current.filter((l) => !same(l, productId, variantId)));
  }, []);

  const clear = useCallback(() => setLines([]), []);

  const value = useMemo<CartApi>(() => ({
    lines,
    itemCount: lines.reduce((sum, l) => sum + l.quantity, 0),
    subtotal: lines.some((l) => l.unitPrice === null)
      ? null
      : lines.reduce((sum, l) => sum + (l.unitPrice ?? 0) * l.quantity, 0),
    add,
    setQuantity,
    remove,
    clear,
  }), [lines, add, setQuantity, remove, clear]);

  return <CartContext.Provider value={value}>{children}</CartContext.Provider>;
}

export function useCart(): CartApi {
  const context = useContext(CartContext);
  if (!context) throw new Error('useCart must be used inside a CartProvider');
  return context;
}

/** For chrome that renders both inside and outside a store, such as the navbar. */
export function useOptionalCart(): CartApi | null {
  return useContext(CartContext);
}
