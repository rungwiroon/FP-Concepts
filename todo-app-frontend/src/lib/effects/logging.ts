import { Effect } from 'effect';
import * as App from '../AppMonad';

export const withLogging = <A>(
  startMessage: string,
  successMessage: (a: A) => string
) =>
  (operation: App.App<A>): App.App<A> =>
    Effect.gen(function* (_) {
      yield* _(App.logInfo(startMessage));
      const result = yield* _(operation);
      yield* _(App.logInfo(successMessage(result)));
      return result;
    });
