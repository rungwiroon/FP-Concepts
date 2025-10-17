# Functional Frontend with fp-ts

A comprehensive guide to building TypeScript frontend applications using functional programming patterns with fp-ts.

## Table of Contents

- [Overview](#overview)
- [Setup](#setup)
- [Core Architecture](#core-architecture)
- [The App Monad](#the-app-monad)
- [Effect Composition](#effect-composition)
- [React Integration](#react-integration)
- [Form Validation](#form-validation)
- [API Client Patterns](#api-client-patterns)
- [State Management](#state-management)
- [Common Patterns](#common-patterns)

---

## Overview

Just like the backend with language-ext, we can compose multiple effects in a type-safe way:

- **API calls** via fetch/axios
- **Logging** to console or services
- **Error handling** with automatic recovery
- **Validation** with error accumulation
- **State management** with predictable updates
- **Caching** for performance
- **Loading states** automatically

### Key Benefits

✅ **Type-safe** - compiler catches errors  
✅ **Composable** - stack effects naturally  
✅ **Testable** - easy to mock dependencies  
✅ **Declarative** - clear intent  
✅ **Error handling** - automatic short-circuiting  
✅ **No callback hell** - flat composition  

---

## Setup

### Install Dependencies

```bash
npm install fp-ts io-ts
# or
yarn add fp-ts io-ts

# Optional but recommended
npm install @devexperts/remote-data-ts
```

### Project Structure

```
src/
├── lib/
│   ├── AppMonad.ts           # App monad implementation
│   ├── AppEnv.ts             # Environment type
│   └── effects/
│       ├── logging.ts
│       ├── api.ts
│       ├── cache.ts
│       └── validation.ts
├── features/
│   └── products/
│       ├── api.ts            # Product API calls
│       ├── validation.ts     # Product validation
│       ├── hooks.ts          # React hooks
│       └── components/
│           ├── ProductList.tsx
│           └── ProductForm.tsx
└── App.tsx
```

---

## Core Architecture

### The AppEnv Type

The environment holds all dependencies:

```typescript
import { TaskEither } from 'fp-ts/TaskEither';
import * as TE from 'fp-ts/TaskEither';
import * as E from 'fp-ts/Either';

interface Logger {
  info: (message: string) => void;
  warn: (message: string) => void;
  error: (message: string, err?: unknown) => void;
}

interface HttpClient {
  get: <A>(url: string) => TaskEither<Error, A>;
  post: <A, B>(url: string, body: A) => TaskEither<Error, B>;
  put: <A, B>(url: string, body: A) => TaskEither<Error, B>;
  delete: (url: string) => TaskEither<Error, void>;
}

interface Cache {
  get: <A>(key: string) => TaskEither<Error, A | null>;
  set: <A>(key: string, value: A, ttl?: number) => TaskEither<Error, void>;
  invalidate: (key: string) => TaskEither<Error, void>;
}

export interface AppEnv {
  httpClient: HttpClient;
  logger: Logger;
  cache?: Cache;  // Optional
}
```

### The App Monad

```typescript
import * as R from 'fp-ts/Reader';
import * as TE from 'fp-ts/TaskEither';
import { pipe } from 'fp-ts/function';

// App<A> = Reader<AppEnv, TaskEither<Error, A>>
// Same structure as: ReaderT<AppEnv, IO, A> in C#
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
  R.ask();

// Lift an async operation
export const fromAsync = <A>(f: () => Promise<A>): App<A> =>
  R.of(TE.tryCatch(
    f,
    (reason) => reason instanceof Error ? reason : new Error(String(reason))
  ));

// Map over the result
export const map = <A, B>(f: (a: A) => B) => 
  (fa: App<A>): App<B> =>
    R.map(TE.map(f))(fa);

// FlatMap for chaining
export const chain = <A, B>(f: (a: A) => App<B>) => 
  (fa: App<A>): App<B> =>
    pipe(
      fa,
      R.chain(te =>
        pipe(
          te,
          TE.chain(a =>
            pipe(
              f(a),
              env => env(ask()(env))
            )
          ),
          R.of
        )
      )
    );

// Run the App with an environment
export const run = <A>(env: AppEnv) => 
  (app: App<A>): TE.TaskEither<Error, A> =>
    app(env);
```

### Helper Functions

```typescript
// Accessing dependencies
export const logger = (): App<Logger> =>
  pipe(
    ask(),
    map(env => env.logger)
  );

export const httpClient = (): App<HttpClient> =>
  pipe(
    ask(),
    map(env => env.httpClient)
  );

export const cache = (): App<Cache | undefined> =>
  pipe(
    ask(),
    map(env => env.cache)
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
```

---

## Effect Composition

### Logging Effect

```typescript
// lib/effects/logging.ts
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

// Usage
const getProduct = (id: number) =>
  pipe(
    fetchProductFromApi(id),
    withLogging(
      `Fetching product ${id}`,
      product => `Successfully fetched: ${product.name}`
    )
  );
```

### Cache Effect

```typescript
// lib/effects/cache.ts
import * as App from '../AppMonad';
import * as TE from 'fp-ts/TaskEither';
import * as O from 'fp-ts/Option';
import { pipe } from 'fp-ts/function';

export const withCache = <A>(
  key: string,
  ttl?: number
) => 
  (operation: App.App<A>): App.App<A> =>
    pipe(
      App.cache(),
      App.chain(cacheOpt =>
        O.fromNullable(cacheOpt).fold(
          // No cache available, just run operation
          () => operation,
          // Cache available
          cache => pipe(
            App.fromTaskEither(cache.get<A>(key)),
            App.chain(cached =>
              cached !== null
                ? pipe(
                    App.logInfo(`Cache hit: ${key}`),
                    App.map(() => cached)
                  )
                : pipe(
                    App.logInfo(`Cache miss: ${key}`),
                    App.chain(() => operation),
                    App.chain(result =>
                      pipe(
                        App.fromTaskEither(cache.set(key, result, ttl)),
                        App.map(() => result)
                      )
                    )
                  )
            )
          )
        )
      )
    );

// Usage
const getProduct = (id: number) =>
  pipe(
    fetchProductFromApi(id),
    withCache(`product:${id}`, 5 * 60 * 1000) // 5 minutes
  );
```

### Retry Effect

```typescript
// lib/effects/retry.ts
import * as App from '../AppMonad';
import * as TE from 'fp-ts/TaskEither';
import { pipe } from 'fp-ts/function';

export const withRetry = (
  maxAttempts: number,
  delayMs: number = 1000
) => 
  <A>(operation: App.App<A>): App.App<A> => {
    const tryExecute = (attempt: number): App.App<A> =>
      attempt > maxAttempts
        ? App.fail(new Error(`Failed after ${maxAttempts} attempts`))
        : pipe(
            operation,
            App.chain(result => App.of(result)),
            // On error, retry
            env => pipe(
              env(operation),
              TE.orElse(err =>
                attempt < maxAttempts
                  ? pipe(
                      App.logInfo(`Retry attempt ${attempt + 1}/${maxAttempts}`),
                      App.chain(() => 
                        App.fromAsync(() => 
                          new Promise(resolve => setTimeout(resolve, delayMs))
                        )
                      ),
                      App.chain(() => tryExecute(attempt + 1))
                    )(env)
                  : TE.left(err)
              )
            )
          );
    
    return tryExecute(1);
  };
```

### Loading State Effect

```typescript
// lib/effects/loading.ts
import * as App from '../AppMonad';
import { pipe } from 'fp-ts/function';

export const withLoadingState = <A>(
  setLoading: (loading: boolean) => void
) => 
  (operation: App.App<A>): App.App<A> =>
    pipe(
      App.fromAsync(() => Promise.resolve(setLoading(true))),
      App.chain(() => operation),
      App.chain(result =>
        pipe(
          App.fromAsync(() => Promise.resolve(setLoading(false))),
          App.map(() => result)
        )
      ),
      // Ensure loading is set to false even on error
      env => pipe(
        env(operation),
        TE.fold(
          err => pipe(
            App.fromAsync(() => Promise.resolve(setLoading(false))),
            App.chain(() => App.fail<A>(err))
          )(env),
          result => pipe(
            App.fromAsync(() => Promise.resolve(setLoading(false))),
            App.map(() => result)
          )(env)
        )
      )
    );
```

---

## API Client Patterns

### Basic HTTP Client

```typescript
// lib/httpClient.ts
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
```

### Product API Example

```typescript
// features/products/api.ts
import * as App from '../../lib/AppMonad';
import { pipe } from 'fp-ts/function';
import { withLogging } from '../../lib/effects/logging';
import { withCache } from '../../lib/effects/cache';
import { withRetry } from '../../lib/effects/retry';

export interface Product {
  id: number;
  name: string;
  price: number;
}

export interface CreateProductRequest {
  name: string;
  price: number;
}

// Get all products
export const listProducts = (): App.App<Product[]> =>
  pipe(
    App.httpClient(),
    App.chain(client => App.fromTaskEither(client.get<Product[]>('/products'))),
    withCache('products:all', 60000), // 1 minute cache
    withLogging(
      'Fetching all products',
      products => `Fetched ${products.length} products`
    )
  );

// Get single product
export const getProduct = (id: number): App.App<Product> =>
  pipe(
    App.httpClient(),
    App.chain(client => 
      App.fromTaskEither(client.get<Product>(`/products/${id}`))
    ),
    withCache(`product:${id}`, 300000), // 5 minute cache
    withRetry(3, 1000), // Retry 3 times with 1s delay
    withLogging(
      `Fetching product ${id}`,
      product => `Fetched product: ${product.name}`
    )
  );

// Create product
export const createProduct = (
  request: CreateProductRequest
): App.App<Product> =>
  pipe(
    App.httpClient(),
    App.chain(client =>
      App.fromTaskEither(client.post<CreateProductRequest, Product>(
        '/products',
        request
      ))
    ),
    withLogging(
      `Creating product: ${request.name}`,
      product => `Created product with ID: ${product.id}`
    )
  );

// Update product
export const updateProduct = (
  id: number,
  request: CreateProductRequest
): App.App<Product> =>
  pipe(
    App.httpClient(),
    App.chain(client =>
      App.fromTaskEither(client.put<CreateProductRequest, Product>(
        `/products/${id}`,
        request
      ))
    ),
    // Invalidate cache after update
    App.chain(product =>
      pipe(
        App.cache(),
        App.chain(cacheOpt =>
          cacheOpt
            ? App.fromTaskEither(cacheOpt.invalidate(`product:${id}`))
            : App.of(undefined)
        ),
        App.map(() => product)
      )
    ),
    withLogging(
      `Updating product ${id}`,
      product => `Updated product: ${product.name}`
    )
  );

// Delete product
export const deleteProduct = (id: number): App.App<void> =>
  pipe(
    App.httpClient(),
    App.chain(client =>
      App.fromTaskEither(client.delete(`/products/${id}`))
    ),
    withLogging(
      `Deleting product ${id}`,
      () => `Deleted product ${id}`
    )
  );
```

---

## Form Validation

Using `io-ts` for runtime validation and `fp-ts` for validation logic:

```typescript
// features/products/validation.ts
import * as t from 'io-ts';
import * as E from 'fp-ts/Either';
import * as A from 'fp-ts/Apply';
import { pipe } from 'fp-ts/function';

// Runtime type validation
export const ProductCodec = t.type({
  name: t.string,
  price: t.number,
});

export type ProductInput = t.TypeOf<typeof ProductCodec>;

// Validation errors
export interface ValidationError {
  field: string;
  message: string;
}

// Validation result that can accumulate errors
export type ValidationResult<A> = E.Either<ValidationError[], A>;

// Individual field validators
const validateName = (name: string): ValidationResult<string> =>
  name.length > 0
    ? E.right(name)
    : E.left([{ field: 'name', message: 'Name is required' }]);

const validatePrice = (price: number): ValidationResult<number> =>
  price > 0
    ? E.right(price)
    : E.left([{ field: 'price', message: 'Price must be greater than 0' }]);

// Combine validators with applicative
export const validateProduct = (
  input: ProductInput
): ValidationResult<ProductInput> =>
  pipe(
    E.Do,
    E.apS('name', validateName(input.name)),
    E.apS('price', validatePrice(input.price)),
    E.map(() => input)
  );

// Alternative: accumulate ALL errors using getValidation
import { getValidation } from 'fp-ts/Either';
import { getSemigroup } from 'fp-ts/Array';

const validationApplicative = getValidation(getSemigroup<ValidationError>());

export const validateProductAll = (
  input: ProductInput
): ValidationResult<ProductInput> =>
  pipe(
    A.sequenceS(validationApplicative)({
      name: validateName(input.name),
      price: validatePrice(input.price),
    }),
    E.map(() => input)
  );
```

---

## React Integration

### Custom Hooks

```typescript
// features/products/hooks.ts
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
```

### React Components

```typescript
// features/products/components/ProductList.tsx
import React from 'react';
import { useAppQuery, type RemoteData } from '../hooks';
import { listProducts, type Product } from '../api';
import type { AppEnv } from '../../../lib/AppEnv';

interface Props {
  env: AppEnv;
}

export const ProductList: React.FC<Props> = ({ env }) => {
  const { state, refetch } = useAppQuery(env, listProducts(), []);

  const renderContent = (state: RemoteData<Error, Product[]>) => {
    switch (state._tag) {
      case 'NotAsked':
        return <div>Not loaded yet</div>;
      
      case 'Loading':
        return <div>Loading products...</div>;
      
      case 'Failure':
        return (
          <div>
            <p>Error: {state.error.message}</p>
            <button onClick={refetch}>Retry</button>
          </div>
        );
      
      case 'Success':
        return (
          <div>
            <h2>Products</h2>
            <button onClick={refetch}>Refresh</button>
            <ul>
              {state.data.map(product => (
                <li key={product.id}>
                  {product.name} - ${product.price}
                </li>
              ))}
            </ul>
          </div>
        );
    }
  };

  return <div className="product-list">{renderContent(state)}</div>;
};
```

```typescript
// features/products/components/ProductForm.tsx
import React, { useState } from 'react';
import { pipe } from 'fp-ts/function';
import * as E from 'fp-ts/Either';
import { useApp } from '../hooks';
import { createProduct, type CreateProductRequest } from '../api';
import { validateProductAll, type ValidationError } from '../validation';
import type { AppEnv } from '../../../lib/AppEnv';

interface Props {
  env: AppEnv;
  onSuccess?: () => void;
}

export const ProductForm: React.FC<Props> = ({ env, onSuccess }) => {
  const [formData, setFormData] = useState<CreateProductRequest>({
    name: '',
    price: 0,
  });
  const [validationErrors, setValidationErrors] = useState<ValidationError[]>([]);
  const { state, execute } = useApp<Product>(env);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    
    // Validate
    const validationResult = validateProductAll(formData);
    
    pipe(
      validationResult,
      E.fold(
        // Validation failed
        (errors) => {
          setValidationErrors(errors);
        },
        // Validation passed
        (validData) => {
          setValidationErrors([]);
          
          // Execute the API call
          execute(createProduct(validData)).then(() => {
            if (state._tag === 'Success') {
              setFormData({ name: '', price: 0 });
              onSuccess?.();
            }
          });
        }
      )
    );
  };

  const getFieldError = (field: string) =>
    validationErrors.find(e => e.field === field)?.message;

  return (
    <form onSubmit={handleSubmit}>
      <div>
        <label>
          Name:
          <input
            type="text"
            value={formData.name}
            onChange={(e) => setFormData({ ...formData, name: e.target.value })}
          />
        </label>
        {getFieldError('name') && (
          <span className="error">{getFieldError('name')}</span>
        )}
      </div>

      <div>
        <label>
          Price:
          <input
            type="number"
            value={formData.price}
            onChange={(e) => setFormData({ ...formData, price: +e.target.value })}
          />
        </label>
        {getFieldError('price') && (
          <span className="error">{getFieldError('price')}</span>
        )}
      </div>

      <button type="submit" disabled={state._tag === 'Loading'}>
        {state._tag === 'Loading' ? 'Creating...' : 'Create Product'}
      </button>

      {state._tag === 'Failure' && (
        <div className="error">Error: {state.error.message}</div>
      )}

      {state._tag === 'Success' && (
        <div className="success">Product created successfully!</div>
      )}
    </form>
  );
};
```

### App Setup

```typescript
// App.tsx
import React from 'react';
import { createHttpClient } from './lib/httpClient';
import type { AppEnv } from './lib/AppEnv';
import { ProductList } from './features/products/components/ProductList';
import { ProductForm } from './features/products/components/ProductForm';

// Create the environment
const env: AppEnv = {
  httpClient: createHttpClient('http://localhost:5000/api'),
  logger: {
    info: (message) => console.info(message),
    warn: (message) => console.warn(message),
    error: (message, err) => console.error(message, err),
  },
  // Optional: add cache if needed
};

export const App: React.FC = () => {
  return (
    <div className="app">
      <h1>Product Management</h1>
      
      <section>
        <h2>Create Product</h2>
        <ProductForm env={env} />
      </section>

      <section>
        <h2>All Products</h2>
        <ProductList env={env} />
      </section>
    </div>
  );
};
```

---

## State Management

### With Context (Recommended for Medium Apps)

```typescript
// lib/AppContext.tsx
import React, { createContext, useContext } from 'react';
import type { AppEnv } from './AppEnv';

const AppContext = createContext<AppEnv | null>(null);

export const AppProvider: React.FC<{ env: AppEnv; children: React.ReactNode }> = ({
  env,
  children,
}) => {
  return <AppContext.Provider value={env}>{children}</AppContext.Provider>;
};

export const useAppEnv = (): AppEnv => {
  const env = useContext(AppContext);
  if (!env) {
    throw new Error('useAppEnv must be used within AppProvider');
  }
  return env;
};

// Updated hooks that use context
export const useAppQuery = <A>(
  app: App.App<A>,
  deps: React.DependencyList = []
) => {
  const env = useAppEnv();
  // ... same implementation as before
};
```

### With Redux Toolkit (For Large Apps)

```typescript
// store/productsSlice.ts
import { createSlice, createAsyncThunk } from '@reduxjs/toolkit';
import * as E from 'fp-ts/Either';
import { pipe } from 'fp-ts/function';
import * as App from '../lib/AppMonad';
import { listProducts, type Product } from '../features/products/api';
import type { AppEnv } from '../lib/AppEnv';

interface ProductsState {
  items: Product[];
  loading: boolean;
  error: string | null;
}

const initialState: ProductsState = {
  items: [],
  loading: false,
  error: null,
};

// Thunk that runs an App operation
export const fetchProducts = createAsyncThunk<
  Product[],
  AppEnv,
  { rejectValue: string }
>(
  'products/fetchProducts',
  async (env, { rejectWithValue }) => {
    const result = await pipe(listProducts(), App.run(env))();
    
    return pipe(
      result,
      E.fold(
        (error) => rejectWithValue(error.message),
        (products) => products
      )
    );
  }
);

export const productsSlice = createSlice({
  name: 'products',
  initialState,
  reducers: {},
  extraReducers: (builder) => {
    builder
      .addCase(fetchProducts.pending, (state) => {
        state.loading = true;
        state.error = null;
      })
      .addCase(fetchProducts.fulfilled, (state, action) => {
        state.loading = false;
        state.items = action.payload;
      })
      .addCase(fetchProducts.rejected, (state, action) => {
        state.loading = false;
        state.error = action.payload ?? 'Unknown error';
      });
  },
});

export default productsSlice.reducer;
```

---

## Common Patterns

### Pattern 1: Sequential Operations

```typescript
// Fetch product, then fetch related items
const getProductWithRelated = (id: number): App.App<ProductWithRelated> =>
  pipe(
    getProduct(id),
    App.chain(product =>
      pipe(
        getRelatedProducts(product.categoryId),
        App.map(related => ({ product, related }))
      )
    )
  );
```

### Pattern 2: Parallel Operations

```typescript
import * as A from 'fp-ts/Array';
import { sequenceT } from 'fp-ts/Apply';

// Fetch multiple products in parallel
const getMultipleProducts = (ids: number[]): App.App<Product[]> =>
  pipe(
    ids,
    A.map(id => getProduct(id)),
    A.sequence(App.Applicative) // Requires defining Applicative instance
  );

// Or using sequenceT for different types
const getDashboardData = (): App.App<DashboardData> =>
  pipe(
    sequenceT(App.Applicative)(
      listProducts(),
      getTotalRevenue(),
      getRecentOrders()
    ),
    App.map(([products, revenue, orders]) => ({
      products,
      revenue,
      orders
    }))
  );
```

### Pattern 3: Conditional Operations

```typescript
const getProductOrCreate = (id: number): App.App<Product> =>
  pipe(
    getProduct(id),
    // On error, create a default product
    env => pipe(
      env(getProduct(id)),
      TE.orElse(err =>
        pipe(
          App.logInfo(`Product ${id} not found, creating default`),
          App.chain(() => createProduct({ name: 'Default', price: 0 }))
        )(env)
      )
    )
  );
```

### Pattern 4: Error Recovery

```typescript
// Try primary, fallback to secondary
const getProductResilient = (id: number): App.App<Product> =>
  pipe(
    getProduct(id),
    env => pipe(
      env(getProduct(id)),
      TE.orElse(() =>
        pipe(
          App.logWarn(`Primary failed, trying cache`),
          App.chain(() => getProductFromCache(id))
        )(env)
      ),
      TE.orElse(() =>
        pipe(
          App.logWarn(`Cache failed, returning default`),
          App.map(() => getDefaultProduct(id))
        )(env)
      )
    )
  );
```

### Pattern 5: Batch Operations with Error Handling

```typescript
// Process array with individual error handling
const createMultipleProducts = (
  requests: CreateProductRequest[]
): App.App<Array<E.Either<Error, Product>>> =>
  pipe(
    requests,
    A.traverse(App.Applicative)(request =>
      pipe(
        createProduct(request),
        App.chain(product => App.of(E.right(product))),
        env => pipe(
          env(createProduct(request)),
          TE.fold(
            err => TE.of(E.left(err)),
            product => TE.of(E.right(product))
          )
        )
      )
    )
  );
```

---

## Best Practices

### 1. Keep Operations Pure

```typescript
// ❌ Bad - side effect hidden
const getProduct = (id: number): App.App<Product> => {
  console.log(`Fetching ${id}`); // Hidden side effect
  return fetchFromApi(id);
};

// ✅ Good - side effect explicit
const getProduct = (id: number): App.App<Product> =>
  pipe(
    App.logInfo(`Fetching product ${id}`),
    App.chain(() => fetchFromApi(id))
  );
```

### 2. Use Type Guards

```typescript
// Type-safe state matching
const renderProductState = (state: RemoteData<Error, Product>) => {
  switch (state._tag) {
    case 'NotAsked':
      return <div>Not loaded</div>;
    case 'Loading':
      return <div>Loading...</div>;
    case 'Failure':
      return <div>Error: {state.error.message}</div>;
    case 'Success':
      return <div>{state.data.name}</div>;
  }
};
```

### 3. Compose Effects Consistently

```typescript
// Always compose in the same order
const apiCallWithEffects = (url: string) =>
  pipe(
    fetchFromApi(url),
    withRetry(3),        // 1. Retry on failure
    withCache(url),      // 2. Cache results
    withLogging('API')   // 3. Log everything
  );
```

### 4. Handle All Error Cases

```typescript
// ❌ Bad - ignoring errors
useEffect(() => {
  App.run(env)(getProducts())();
}, []);

// ✅ Good - handling errors
useEffect(() => {
  App.run(env)(getProducts())().then(
    E.fold(
      err => console.error('Failed to load products:', err),
      products => console.log('Loaded products:', products)
    )
  );
}, []);
```

### 5. Extract Reusable Operations

```typescript
// Create reusable composed operations
const createApiCall = <A>(
  name: string,
  operation: App.App<A>
): App.App<A> =>
  pipe(
    operation,
    withRetry(3),
    withCache(name, 60000),
    withLogging(`API: ${name}`, () => `${name} completed`)
  );

// Use it
const getProduct = (id: number) =>
  createApiCall(
    `get-product-${id}`,
    pipe(
      App.httpClient(),
      App.chain(client => 
        App.fromTaskEither(client.get<Product>(`/products/${id}`))
      )
    )
  );
```

---

## Comparison: Ramda vs fp-ts

### Ramda Approach

Ramda is great for **data transformation** but lacks type safety for effects:

```typescript
import * as R from 'ramda';

// Ramda - good for transformations
const processProducts = R.pipe(
  R.filter(R.propSatisfies(R.gt(R.__, 100), 'price')),
  R.map(R.pick(['id', 'name', 'price'])),
  R.sortBy(R.prop('price'))
);

// But handling async/effects is not as elegant
const fetchAndProcess = async (ids: number[]) => {
  const products = await Promise.all(ids.map(fetchProduct));
  return processProducts(products);
};
```

### fp-ts Approach

fp-ts provides **type-safe effect composition**:

```typescript
import { pipe } from 'fp-ts/function';
import * as A from 'fp-ts/Array';

// fp-ts - type-safe effects
const fetchAndProcess = (ids: number[]): App.App<Product[]> =>
  pipe(
    ids,
    A.traverse(App.Applicative)(fetchProduct),
    App.map(products =>
      pipe(
        products,
        A.filter(p => p.price > 100),
        A.map(p => ({ id: p.id, name: p.name, price: p.price })),
        A.sortBy([Ord.contramap((p: Product) => p.price)(N.Ord)])
      )
    )
  );
```

**Recommendation:** Use both!
- **Ramda** for pure data transformations
- **fp-ts** for effect composition and type safety

---

## Resources

- [fp-ts Documentation](https://gcanti.github.io/fp-ts/)
- [io-ts for Runtime Type Checking](https://github.com/gcanti/io-ts)
- [Functional Programming in TypeScript](https://github.com/enricopolanski/functional-programming)
- [Remote Data Pattern](https://github.com/devexperts/remote-data-ts)

---

## Conclusion

The functional patterns from backend (language-ext) translate beautifully to frontend (fp-ts):

| Backend (C#/language-ext) | Frontend (TS/fp-ts) |
|---------------------------|---------------------|
| `Db<A>` | `App<A>` |
| `DbEnv` | `AppEnv` |
| `ReaderT<DbEnv, IO, A>` | `Reader<AppEnv, TaskEither<Error, A>>` |
| `WithTransaction()` | `withRetry()`, `withCache()` |
| `ProductRepository` | Product API module |
| ASP.NET endpoints | React hooks |

The benefits are the same: **type safety, composability, testability, and maintainability**! 🚀