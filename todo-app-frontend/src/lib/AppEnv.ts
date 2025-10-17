import { TaskEither } from 'fp-ts/TaskEither';

export interface Logger {
  info: (message: string) => void;
  warn: (message: string) => void;
  error: (message: string, err?: unknown) => void;
}

export interface HttpClient {
  get: <A>(url: string) => TaskEither<Error, A>;
  post: <A, B>(url: string, body: A) => TaskEither<Error, B>;
  put: <A, B>(url: string, body: A) => TaskEither<Error, B>;
  patch: <A>(url: string) => TaskEither<Error, A>;
  delete: (url: string) => TaskEither<Error, void>;
}

export interface AppEnv {
  httpClient: HttpClient;
  logger: Logger;
  baseUrl: string;
}
