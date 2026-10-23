/** Strips formatting so a number can be used in a wa.me link. */
export function sanitizePhone(phone: string): string {
  return phone.replace(/[^\d]/g, '');
}

/**
 * Opens a WhatsApp chat with the number and drops `message` into the chat's text box, ready to
 * send. Works in WhatsApp Web and hands off to the native app on phones.
 */
export function whatsappLink(phone: string, message?: string): string {
  const base = `https://wa.me/${sanitizePhone(phone)}`;
  return message ? `${base}?text=${encodeURIComponent(message)}` : base;
}

export function callLink(phone: string): string {
  return `tel:${phone.replace(/\s+/g, '')}`;
}
