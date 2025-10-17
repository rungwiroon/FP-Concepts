import * as R from 'fp-ts/Reader';
import * as TE from 'fp-ts/TaskEither';
import { pipe } from 'fp-ts/function';
import type { AppEnv } from './AppEnv';

// App<A> = Reader<AppEnv, TaskEither<Error, A>>
export type App<A> = R.Reader<AppEnv, TE.TaskEither<Error, A>>;

// Basic constructors
export const of = <A>(a: A): App<A> => 
  R.of(TE.of(a));

export const fail = <A>(error: Error): App<A> => 
  R.of(TE.left(error));

// Lift a TaskEither into App
export const fromTaskEither = <A>(te: TE.TaskEither<Error, A>): App<A> =>
  R.of(te);

// Access the environment
export const ask = (): App<AppEnv> =>
  R.asks((env: AppEnv) => TE.of(env));

// Lift an async operation
export const fromAsync = <A>(f: () => Promise<A>): App<A> =>
  R.of(TE.tryCatch(
    f,
    (reason) => reason instanceof Error ? reason : new Error(String(reason))
  ));

// Map over the result
export const map = <A, B>(f: (a: A) => B) => 
  (fa: App<A>): App<B> =>
    pipe(fa, R.map(TE.map(f)));

// FlatMap for chaining
export const chain = <A, B>(f: (a: A) => App<B>) => 
  (fa: App<A>): App<B> =>
    (env: AppEnv) =>
      pipe(
        fa(env),
        TE.chain(a => f(a)(env))
      );

// Run the App with an environment
export const run = <A>(env: AppEnv) => 
  (app: App<A>): TE.TaskEither<Error, A> =>
    app(env);

// Accessing dependencies
export const logger = (): App<AppEnv['logger']> =>
  pipe(
    ask(),
    map(env => env.logger)
  );

export const httpClient = (): App<AppEnv['httpClient']> =>
  pipe(
    ask(),
    map(env => env.httpClient)
  );

// Logging operations
export const logInfo = (message: string): App<void> =>
  pipe(
    logger(),
    chain(log => fromAsync(() => Promise.resolve(log.info(message))))
  );

export const logError = (message: string, err?: unknown): App<void> =>
  pipe(
    logger(),
    chain(log => fromAsync(() => Promise.resolve(log.error(message, err))))
  );

export const logWarn = (message: string): App<void> =>
  pipe(
    logger(),
    chain(log => fromAsync(() => Promise.resolve(log.warn(message))))
  );
