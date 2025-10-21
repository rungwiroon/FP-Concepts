# fp-ts to Effect Migration Guide

This document outlines the migration from `fp-ts` to `effect` in the todo-app-frontend.

## Overview

The migration replaced fp-ts's functional programming abstractions with Effect, a more modern and powerful functional effect system for TypeScript.

### Key Changes

| fp-ts | Effect | Notes |
|-------|--------|-------|
| `TaskEither<E, A>` | `Effect.Effect<A, E, R>` | Effect combines async operations, error handling, and dependency injection |
| `Reader<R, A>` | Built into `Effect<A, E, R>` | The `R` type parameter handles dependencies |
| `Either<E, A>` | `Either.Either<A, E>` | Note: type parameters are swapped! |
| `pipe()` | `.pipe()` or `Effect.gen()` | Effect supports both styles, with gen being more ergonomic |
| `chain()` | `flatMap()` | Effect uses standard functional naming |

## Migration Details

### 1. Dependencies

**Before (package.json):**
```json
{
  "fp-ts": "^2.16.9",
  "io-ts": "^2.2.21"
}
```

**After (package.json):**
```json
{
  "effect": "^3.10.0"
}
```

### 2. AppEnv.ts - Dependency Injection

**Before:**
```typescript
import { TaskEither } from 'fp-ts/TaskEither';

export interface HttpClient {
  get: <A>(url: string) => TaskEither<Error, A>;
  // ...
}

export interface AppEnv {
  httpClient: HttpClient;
  logger: Logger;
  baseUrl: string;
}
```

**After:**
```typescript
import { Context, Effect } from 'effect';

export interface HttpClient {
  get: <A>(url: string) => Effect.Effect<A, Error>;
  // ...
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
```

**Benefits:**
- Context.Tag provides type-safe dependency injection
- Better integration with Effect's service layer
- No manual environment passing needed

### 3. HttpClient.ts - Async Operations

**Before:**
```typescript
import * as TE from 'fp-ts/TaskEither';

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
  // ...
});
```

**After:**
```typescript
import { Effect } from 'effect';

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
  // ...
});
```

**Benefits:**
- More explicit error handling with `tryPromise`
- Better type inference
- Cleaner syntax with object configuration

### 4. AppMonad.ts - Core Abstraction

**Before:**
```typescript
import * as R from 'fp-ts/Reader';
import * as TE from 'fp-ts/TaskEither';
import { pipe } from 'fp-ts/function';

// App<A> = Reader<AppEnv, TaskEither<Error, A>>
export type App<A> = R.Reader<AppEnv, TE.TaskEither<Error, A>>;

export const of = <A>(a: A): App<A> =>
  R.of(TE.of(a));

export const chain = <A, B>(f: (a: A) => App<B>) =>
  (fa: App<A>): App<B> =>
    (env: AppEnv) =>
      pipe(
        fa(env),
        TE.chain(a => f(a)(env))
      );
```

**After:**
```typescript
import { Effect } from 'effect';
import { HttpClientService, LoggerService, type AppEnv } from './AppEnv';

// App<A> = Effect that requires HttpClient and Logger services
export type App<A> = Effect.Effect<A, Error, HttpClientService | LoggerService>;

export const succeed = <A>(a: A): App<A> =>
  Effect.succeed(a);

// Re-export common Effect utilities
export const flatMap = Effect.flatMap;
export const gen = Effect.gen;
```

**Benefits:**
- Effect.Effect<A, E, R> naturally combines Reader + TaskEither
- No manual composition needed
- Better type inference and error messages
- Access to Effect's rich ecosystem (scheduling, resources, etc.)

### 5. API Layer - Using Effect.gen

**Before:**
```typescript
import * as App from '../../lib/AppMonad';
import { pipe } from 'fp-ts/function';

export const listTodos = (): App.App<Todo[]> =>
  pipe(
    App.httpClient(),
    App.chain(client => App.fromTaskEither(client.get<Todo[]>('/todos'))),
    withLogging(
      'Fetching all todos',
      todos => `Fetched ${todos.length} todos`
    )
  );
```

**After:**
```typescript
import { Effect } from 'effect';
import * as App from '../../lib/AppMonad';

export const listTodos = (): App.App<Todo[]> =>
  Effect.gen(function* (_) {
    const client = yield* _(App.httpClient());
    return yield* _(client.get<Todo[]>('/todos'));
  }).pipe(
    withLogging(
      'Fetching all todos',
      todos => `Fetched ${todos.length} todos`
    )
  );
```

**Benefits:**
- `Effect.gen` provides imperative-style syntax with generator functions
- More readable than deeply nested pipes
- Still fully type-safe
- Easy to add logging, error handling, etc.

### 6. Validation - Effect's Either

**Before:**
```typescript
import * as E from 'fp-ts/Either';
import * as A from 'fp-ts/Apply';
import { pipe } from 'fp-ts/function';
import { getValidation } from 'fp-ts/Either';
import { getSemigroup } from 'fp-ts/Array';

export type ValidationResult<A> = E.Either<ValidationError[], A>;

const validationApplicative = getValidation(getSemigroup<ValidationError>());

export const validateTodo = (
  input: CreateTodoRequest
): ValidationResult<CreateTodoRequest> =>
  pipe(
    A.sequenceS(validationApplicative)({
      title: validateTitle(input.title),
      description: validateDescription(input.description),
    }),
    E.map(() => input)
  );
```

**After:**
```typescript
import { Either } from 'effect';

export type ValidationResult<A> = Either.Either<A, ValidationError[]>;

export const validateTodo = (
  input: CreateTodoRequest
): ValidationResult<CreateTodoRequest> => {
  const titleResult = validateTitle(input.title);
  const descResult = validateDescription(input.description);

  if (Either.isLeft(titleResult) || Either.isLeft(descResult)) {
    const errors: ValidationError[] = [
      ...(Either.isLeft(titleResult) ? titleResult.left : []),
      ...(Either.isLeft(descResult) ? descResult.left : []),
    ];
    return Either.left(errors);
  }

  return Either.right(input);
};
```

**Note:** Effect's Either has **swapped type parameters**: `Either<A, E>` instead of `Either<E, A>`

### 7. React Hooks - Running Effects

**Before:**
```typescript
import * as E from 'fp-ts/Either';
import { pipe } from 'fp-ts/function';

const result = await pipe(app, App.run(env))();

pipe(
  result,
  E.fold(
    (error) => setState(failure(error)),
    (data) => setState(success(data))
  )
);
```

**After:**
```typescript
import { Effect, Exit } from 'effect';

const provideServices = <A>(
  effect: App<A>,
  env: AppEnv
): Effect.Effect<A, Error> =>
  effect.pipe(
    Effect.provideService(HttpClientService, env.httpClient),
    Effect.provideService(LoggerService, env.logger)
  ) as Effect.Effect<A, Error>;

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
```

**Benefits:**
- `Effect.runPromiseExit` provides structured error information
- Services are provided explicitly using `provideService`
- Better error tracking and debugging

## Common Patterns

### Pattern 1: Sequential Operations

**fp-ts (pipe + chain):**
```typescript
pipe(
  getUser(id),
  App.chain(user => getProfile(user.id)),
  App.chain(profile => updateProfile(profile))
)
```

**Effect (gen):**
```typescript
Effect.gen(function* (_) {
  const user = yield* _(getUser(id));
  const profile = yield* _(getProfile(user.id));
  return yield* _(updateProfile(profile));
})
```

### Pattern 2: Error Handling

**fp-ts:**
```typescript
pipe(
  operation,
  TE.orElse(err => TE.right(defaultValue))
)
```

**Effect:**
```typescript
operation.pipe(
  Effect.catchAll(err => Effect.succeed(defaultValue))
)
```

### Pattern 3: Logging/Tracing

**fp-ts:**
```typescript
pipe(
  operation,
  App.chain(result =>
    pipe(
      App.logInfo(`Result: ${result}`),
      App.map(() => result)
    )
  )
)
```

**Effect:**
```typescript
operation.pipe(
  Effect.tap(result => App.logInfo(`Result: ${result}`))
)
```

## Benefits of Effect

1. **Better Type Inference**: Effect has superior type inference, reducing the need for explicit type annotations
2. **Unified API**: One Effect type instead of multiple monad compositions
3. **Generator Syntax**: More readable imperative-style code with full type safety
4. **Rich Ecosystem**: Built-in support for resources, scheduling, metrics, tracing, etc.
5. **Better Performance**: Effect is optimized for performance with lazy evaluation
6. **Improved DX**: Better error messages and IDE support

## Breaking Changes

1. **Either type parameters are swapped**: `Either<A, E>` instead of `Either<E, A>`
2. **Different function names**: `chain` → `flatMap`, `of` → `succeed`
3. **Service injection**: Use Context.Tag instead of manual Reader pattern
4. **Running effects**: Use `Effect.runPromise` instead of calling TaskEither as a function

## Migration Checklist

- [x] Replace `fp-ts` and `io-ts` dependencies with `effect`
- [x] Update `AppEnv` to use Context.Tag for services
- [x] Convert `TaskEither` to `Effect` in HttpClient
- [x] Simplify `AppMonad` using Effect's built-in capabilities
- [x] Update all API functions to use `Effect.gen`
- [x] Convert validation to use Effect's Either
- [x] Update React hooks to run Effects with `runPromiseExit`
- [x] Update all components using Either (note swapped type params)
- [x] Remove all `fp-ts` imports
- [x] Test the application builds successfully
- [x] Verify all functionality works as expected

## Resources

- [Effect Documentation](https://effect.website/)
- [Effect GitHub](https://github.com/Effect-TS/effect)
- [Effect Discord Community](https://discord.gg/effect-ts)
