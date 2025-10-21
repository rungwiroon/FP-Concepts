import { Effect } from 'effect';
import { HttpClientService, LoggerService, type AppEnv } from './AppEnv';

// App<A> = Effect that requires HttpClient and Logger services, may fail with Error, and succeeds with A
export type App<A> = Effect.Effect<A, Error, HttpClientService | LoggerService>;

// Basic constructors
export const succeed = <A>(a: A): App<A> =>
  Effect.succeed(a);

export const fail = <A = never>(error: Error): App<A> =>
  Effect.fail(error);

// Lift a promise into App
export const fromPromise = <A>(f: () => Promise<A>): App<A> =>
  Effect.tryPromise({
    try: f,
    catch: (reason) => reason instanceof Error ? reason : new Error(String(reason))
  });

// Access the logger service
export const logger = (): App<AppEnv['logger']> =>
  LoggerService;

// Access the http client service
export const httpClient = (): App<AppEnv['httpClient']> =>
  HttpClientService;

// Logging operations
export const logInfo = (message: string): App<void> =>
  Effect.gen(function* (_) {
    const log = yield* _(LoggerService);
    log.info(message);
  });

export const logError = (message: string, err?: unknown): App<void> =>
  Effect.gen(function* (_) {
    const log = yield* _(LoggerService);
    log.error(message, err);
  });

export const logWarn = (message: string): App<void> =>
  Effect.gen(function* (_) {
    const log = yield* _(LoggerService);
    log.warn(message);
  });

// Re-export common Effect utilities for convenience
export const map = Effect.map;
export const flatMap = Effect.flatMap;
export const gen = Effect.gen;
export const all = Effect.all;
export const catchAll = Effect.catchAll;
export const tap = Effect.tap;
