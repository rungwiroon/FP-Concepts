import * as TE from 'fp-ts/TaskEither';
import type { HttpClient } from './AppEnv';

export const createHttpClient = (baseURL: string): HttpClient => ({
  get: <A>(url: string) =>
    TE.tryCatch(
      async () => {
        const response = await fetch(`${baseURL}${url}`);
        if (!response.ok) {
          throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }
        return response.json() as Promise<A>;
      },
      (reason) => reason instanceof Error ? reason : new Error(String(reason))
    ),

  post: <A, B>(url: string, body: A) =>
    TE.tryCatch(
      async () => {
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
      (reason) => reason instanceof Error ? reason : new Error(String(reason))
    ),

  put: <A, B>(url: string, body: A) =>
    TE.tryCatch(
      async () => {
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
      (reason) => reason instanceof Error ? reason : new Error(String(reason))
    ),

  patch: <A>(url: string) =>
    TE.tryCatch(
      async () => {
        const response = await fetch(`${baseURL}${url}`, {
          method: 'PATCH',
        });
        if (!response.ok) {
          throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }
        return response.json() as Promise<A>;
      },
      (reason) => reason instanceof Error ? reason : new Error(String(reason))
    ),

  delete: (url: string) =>
    TE.tryCatch(
      async () => {
        const response = await fetch(`${baseURL}${url}`, {
          method: 'DELETE',
        });
        if (!response.ok) {
          throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }
      },
      (reason) => reason instanceof Error ? reason : new Error(String(reason))
    ),
});
