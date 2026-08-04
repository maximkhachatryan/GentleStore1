export function sanitizePhone(phone: string): string {
  return phone.replace(/[^\d]/g, '');
}

export function whatsappLink(phone: string, message: string): string {
  return `https://wa.me/${sanitizePhone(phone)}?text=${encodeURIComponent(message)}`;
}

export function callLink(phone: string): string {
  return `tel:${phone.replace(/\s+/g, '')}`;
}
