// The console builds wa.me links too (to send invite links), so the implementation lives in the
// shared package. Re-exported here to keep the storefront's imports local.
export { callLink, sanitizePhone, whatsappLink } from '@gentlestore/shared';
