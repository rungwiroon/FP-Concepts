import { useState, useEffect, useCallback } from 'react';
import * as App from '../../lib/AppMonad';
import * as E from 'fp-ts/Either';
import { pipe } from 'fp-ts/function';
import type { AppEnv } from '../../lib/AppEnv';

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

// Hook to run an App operation
export const useApp = <A>(env: AppEnv) => {
  const [state, setState] = useState<RemoteData<Error, A>>(notAsked());

  const execute = useCallback(
    async (app: App.App<A>) => {
      setState(loading());

      const result = await pipe(
        app,
        App.run(env)
      )();

      pipe(
        result,
        E.fold(
          (error) => setState(failure(error)),
          (data) => setState(success(data))
        )
      );
    },
    [env]
  );

  return { state, execute };
};

// Hook for fetching data on mount
export const useAppQuery = <A>(
  env: AppEnv,
  app: App.App<A>,
  deps: React.DependencyList = []
) => {
  const [state, setState] = useState<RemoteData<Error, A>>(loading());

  useEffect(() => {
    let cancelled = false;

    const fetchData = async () => {
      const result = await pipe(app, App.run(env))();

      if (!cancelled) {
        pipe(
          result,
          E.fold(
            (error) => setState(failure(error)),
            (data) => setState(success(data))
          )
        );
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
    const result = await pipe(app, App.run(env))();

    pipe(
      result,
      E.fold(
        (error) => setState(failure(error)),
        (data) => setState(success(data))
      )
    );
  }, [app, env]);

  return { state, refetch };
};
