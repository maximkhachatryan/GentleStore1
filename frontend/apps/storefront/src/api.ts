import { createApi, createHttp } from '@gentlestore/shared';

/** The platform storefront's host. Unset in development, where every host counts as the platform. */
const PLATFORM_HOST: string | undefined = import.meta.env.VITE_STOREFRONT_URL
  ? new URL(import.meta.env.VITE_STOREFRONT_URL).hostname
  : undefined;
const HOST = window.location.hostname;

/**
 * True when the app was opened on a store's own domain (flowers.am) rather than the platform's
 * (example.com/flowers). `localhost` covers both the dev server and the Capacitor app.
 */
export const ON_STORE_DOMAIN = !!PLATFORM_HOST && HOST !== 'localhost' && HOST !== PLATFORM_HOST;

/** True on the deployed platform domain itself — not a store's own, not development, not the app. */
export const ON_PLATFORM_DOMAIN = !!PLATFORM_HOST && HOST === PLATFORM_HOST;

// On a store's own domain the API is reached through that same domain (Caddy routes /api/public
// and /uploads there), which keeps the session cookie first-party — a cookie set by the platform's
// API host would be a third-party one there, and browsers would not send it.
export const API_URL = ON_STORE_DOMAIN ? '' : (import.meta.env.VITE_API_URL ?? 'http://localhost:5080');

// withCredentials: the storefront's customer session lives in an HttpOnly cookie the API sets,
// which the browser only sends on credentialed cross-origin requests.
export const http = createHttp(API_URL, { withCredentials: true });
export const api = createApi(http);
