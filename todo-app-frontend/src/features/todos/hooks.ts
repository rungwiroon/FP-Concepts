import { useState, useEffect, useCallback } from 'react';
import { Effect, Exit } from 'effect';
import type { App } from '../../lib/AppMonad';
import { HttpClientService, LoggerService, type AppEnv } from '../../lib/AppEnv';

// RemoteData pattern for loading states
export type RemoteData<E, A> =
  | { _tag: 'NotAsked' }
  | { _tag: 'Loading' }
  | { _tag: 'Failure'; error: E }
  | { _tag: 'Success'; data: A };

export const notAsked = <E, A>(): RemoteData<E, A> => ({ _tag: 'NotAsked' });
export const loading = <E, A>(): RemoteData<E, A> => ({ _tag: 'Loading' });
export const failure = <E, A>(error: E): RemoteData<E, A> => ({
  _tag: 'Failure',
  error
});
export const success = <E, A>(data: A): RemoteData<E, A> => ({
  _tag: 'Success',
  data
});

// Helper to provide services to an Effect
const provideServices = <A>(
  effect: App<A>,
  env: AppEnv
): Effect.Effect<A, Error> =>
  effect.pipe(
    Effect.provideService(HttpClientService, env.httpClient),
    Effect.provideService(LoggerService, env.logger)
  ) as Effect.Effect<A, Error>;

// Hook to run an App operation
export const useApp = <A>(env: AppEnv) => {
  const [state, setState] = useState<RemoteData<Error, A>>(notAsked());

  const execute = useCallback(
    async (app: App<A>) => {
      setState(loading());

      const result = await Effect.runPromiseExit(
        provideServices(app, env)
      );

      if (Exit.isSuccess(result)) {
        setState(success(result.value));
      } else {
        const error = result.cause._tag === 'Fail'
          ? result.cause.error
          : new Error('Unknown error');
        setState(failure(error));
      }
    },
    [env]
  );

  return { state, execute };
};

// Hook for fetching data on mount
export const useAppQuery = <A>(
  env: AppEnv,
  app: App<A>,
  deps: React.DependencyList = []
) => {
  const [state, setState] = useState<RemoteData<Error, A>>(loading());

  useEffect(() => {
    let cancelled = false;

    const fetchData = async () => {
      const result = await Effect.runPromiseExit(
        provideServices(app, env)
      );

      if (!cancelled) {
        if (Exit.isSuccess(result)) {
          setState(success(result.value));
        } else {
          const error = result.cause._tag === 'Fail'
            ? result.cause.error
            : new Error('Unknown error');
          setState(failure(error));
        }
      }
    };

    fetchData();

    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, deps);

  const refetch = useCallback(async () => {
    setState(loading());
    const result = await Effect.runPromiseExit(
      provideServices(app, env)
    );

    if (Exit.isSuccess(result)) {
      setState(success(result.value));
    } else {
      const error = result.cause._tag === 'Fail'
        ? result.cause.error
        : new Error('Unknown error');
      setState(failure(error));
    }
  }, [app, env]);

  return { state, refetch };
};
