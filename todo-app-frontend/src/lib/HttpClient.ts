import { Effect } from 'effect';
import type { HttpClient } from './AppEnv';

export const createHttpClient = (baseURL: string): HttpClient => ({
  get: <A>(url: string) =>
    Effect.tryPromise({
      try: async () => {
        const response = await fetch(`${baseURL}${url}`);
        if (!response.ok) {
          throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }
        return response.json() as Promise<A>;
      },
      catch: (reason) => reason instanceof Error ? reason : new Error(String(reason))
    }),

  post: <A, B>(url: string, body: A) =>
    Effect.tryPromise({
      try: async () => {
        const response = await fetch(`${baseURL}${url}`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(body),
        });
        if (!response.ok) {
          throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }
        return response.json() as Promise<B>;
      },
      catch: (reason) => reason instanceof Error ? reason : new Error(String(reason))
    }),

  put: <A, B>(url: string, body: A) =>
    Effect.tryPromise({
      try: async () => {
        const response = await fetch(`${baseURL}${url}`, {
          method: 'PUT',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(body),
        });
        if (!response.ok) {
          throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }
        return response.json() as Promise<B>;
      },
      catch: (reason) => reason instanceof Error ? reason : new Error(String(reason))
    }),

  patch: <A>(url: string) =>
    Effect.tryPromise({
      try: async () => {
        const response = await fetch(`${baseURL}${url}`, {
          method: 'PATCH',
        });
        if (!response.ok) {
          throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }
        return response.json() as Promise<A>;
      },
      catch: (reason) => reason instanceof Error ? reason : new Error(String(reason))
    }),

  delete: (url: string) =>
    Effect.tryPromise({
      try: async () => {
        const response = await fetch(`${baseURL}${url}`, {
          method: 'DELETE',
        });
        if (!response.ok) {
          throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }
      },
      catch: (reason) => reason instanceof Error ? reason : new Error(String(reason))
    }),
});
