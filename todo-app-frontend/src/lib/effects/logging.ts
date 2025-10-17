import * as App from '../AppMonad';
import { pipe } from 'fp-ts/function';

export const withLogging = <A>(
  startMessage: string,
  successMessage: (a: A) => string
) => 
  (operation: App.App<A>): App.App<A> =>
    pipe(
      App.logInfo(startMessage),
      App.chain(() => operation),
      App.chain(result =>
        pipe(
          App.logInfo(successMessage(result)),
          App.map(() => result)
        )
      )
    );
