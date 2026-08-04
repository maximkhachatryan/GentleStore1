import axios from 'axios';
import type { AxiosInstance, InternalAxiosRequestConfig } from 'axios';
import { tokenStore } from './token';

export function createHttp(baseUrl: string, onUnauthorized?: () => void): AxiosInstance {
  const http = axios.create({ baseURL: baseUrl });

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
        onUnauthorized?.();
      }
      return Promise.reject(error);
    },
  );

  return http;
}
