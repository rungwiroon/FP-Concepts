import { Context, Effect } from 'effect';

export interface Logger {
  info: (message: string) => void;
  warn: (message: string) => void;
  error: (message: string, err?: unknown) => void;
}

export interface HttpClient {
  get: <A>(url: string) => Effect.Effect<A, Error>;
  post: <A, B>(url: string, body: A) => Effect.Effect<B, Error>;
  put: <A, B>(url: string, body: A) => Effect.Effect<B, Error>;
  patch: <A>(url: string) => Effect.Effect<A, Error>;
  delete: (url: string) => Effect.Effect<void, Error>;
}

// Define Context Tags for dependency injection
export class LoggerService extends Context.Tag('LoggerService')<
  LoggerService,
  Logger
>() {}

export class HttpClientService extends Context.Tag('HttpClientService')<
  HttpClientService,
  HttpClient
>() {}

export class BaseUrl extends Context.Tag('BaseUrl')<BaseUrl, string>() {}

// Combined environment for convenience
export interface AppEnv {
  httpClient: HttpClient;
  logger: Logger;
  baseUrl: string;
}
