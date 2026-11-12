export function formatPrice(amount: number, currency: string): string {
  try {
    return new Intl.NumberFormat(undefined, { style: 'currency', currency }).format(amount);
  } catch {
    return `${currency} ${amount.toFixed(2)}`;
  }
}

/**
 * The saving as a whole percentage, for a "−20%" badge. Null when there is no sale, or when it
 * rounds to nothing — a "−0%" badge would look broken.
 */
export function discountPercent(price: number | null, regularPrice: number | null | undefined): number | null {
  if (price === null || regularPrice == null || regularPrice <= 0 || price >= regularPrice) return null;
  const percent = Math.round((1 - price / regularPrice) * 100);
  return percent > 0 ? percent : null;
}
