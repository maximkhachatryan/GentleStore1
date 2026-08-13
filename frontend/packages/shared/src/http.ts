import axios from 'axios';
import type { AxiosInstance, InternalAxiosRequestConfig } from 'axios';
import { tokenStore } from './token';

interface HttpOptions {
  onUnauthorized?: () => void;
  /**
   * Send and accept cookies cross-origin. The storefront needs this for its customer session
   * cookie; the API must answer with `Access-Control-Allow-Credentials` and a concrete origin.
   */
  withCredentials?: boolean;
}

export function createHttp(baseUrl: string, options: HttpOptions = {}): AxiosInstance {
  const http = axios.create({ baseURL: baseUrl, withCredentials: options.withCredentials ?? false });

  http.interceptors.request.use((config: InternalAxiosRequestConfig) => {
    const token = tokenStore.get();
    if (token) {
      config.headers.set('Authorization', `Bearer ${token}`);
    }
    return config;
  });

  http.interceptors.response.use(
    (response) => response,
    (error) => {
      if (error?.response?.status === 401) {
        tokenStore.clear();
        options.onUnauthorized?.();
      }
      return Promise.reject(error);
    },
  );

  return http;
}
