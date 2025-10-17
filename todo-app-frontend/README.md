# Functional Todo App - Frontend

A React TypeScript frontend built with **fp-ts** following functional programming principles. This app connects to the ASP.NET Core backend built with **language-ext**.

## Features

- **Functional Programming with fp-ts**
  - App monad for effect composition
  - Type-safe HTTP client
  - RemoteData pattern for loading states
  - Applicative validation with error accumulation
  - Composable effects (logging, etc.)

- **Modern React with TypeScript**
  - React 18 with hooks
  - TypeScript strict mode
  - Vite for fast development
  - Clean component architecture

## Project Structure

```
src/
├── lib/
│   ├── AppEnv.ts              # Environment type definition
│   ├── AppMonad.ts            # App monad implementation
│   ├── HttpClient.ts          # HTTP client with fp-ts
│   └── effects/
│       └── logging.ts         # Logging effect
├── features/
│   └── todos/
│       ├── types.ts           # Todo types
│       ├── api.ts             # Todo API calls
│       ├── validation.ts      # Todo validation
│       ├── hooks.ts           # React hooks
│       └── components/
│           ├── TodoList.tsx   # Todo list component
│           └── TodoForm.tsx   # Todo form component
├── App.tsx                    # Main app component
├── main.tsx                   # Entry point
├── App.css                    # Styles
└── index.css                  # Global styles
```

## Getting Started

### Prerequisites

- Node.js 18+ and npm
- Backend API running on `http://localhost:5000`

### Installation

```bash
cd todo-app-frontend
npm install
```

### Development

```bash
npm run dev
```

The app will be available at `http://localhost:3000`

### Build for Production

```bash
npm run build
npm run preview
```

## Functional Programming Concepts

### 1. App Monad

The `App<A>` monad combines Reader and TaskEither:

```typescript
type App<A> = Reader<AppEnv, TaskEither<Error, A>>
```

This allows us to:
- Access environment dependencies (HttpClient, Logger)
- Handle async operations
- Manage errors type-safely

### 2. Effect Composition

Operations can be composed with effects:

```typescript
pipe(
  fetchTodos(),
  withLogging('Fetching todos', todos => `Loaded ${todos.length} todos`)
)
```

### 3. RemoteData Pattern

Loading states are modeled as a discriminated union:

```typescript
type RemoteData<E, A> =
  | { _tag: 'NotAsked' }
  | { _tag: 'Loading' }
  | { _tag: 'Failure'; error: E }
  | { _tag: 'Success'; data: A }
```

### 4. Applicative Validation

Validation accumulates all errors:

```typescript
pipe(
  sequenceS(validationApplicative)({
    title: validateTitle(input.title),
    description: validateDescription(input.description),
  }),
  map(() => input)
)
```

### 5. Type-Safe HTTP Client

All API calls return `TaskEither<Error, A>`:

```typescript
get: <A>(url: string) => TaskEither<Error, A>
post: <A, B>(url: string, body: A) => TaskEither<Error, B>
```

## API Integration

The app expects the backend API to be running at `http://localhost:5000` with the following endpoints:

- `GET /todos` - List all todos
- `GET /todos/:id` - Get todo by ID
- `POST /todos` - Create new todo
- `PUT /todos/:id` - Update todo
- `PATCH /todos/:id/toggle` - Toggle completion
- `DELETE /todos/:id` - Delete todo

## Key Dependencies

- **react** - UI library
- **fp-ts** - Functional programming library
- **io-ts** - Runtime type checking
- **vite** - Build tool
- **typescript** - Type safety

## Comparison to Backend

| Backend (C#/language-ext) | Frontend (TS/fp-ts) |
|---------------------------|---------------------|
| `Db<A>` | `App<A>` |
| `DbEnv` | `AppEnv` |
| `ReaderT<DbEnv, IO, A>` | `Reader<AppEnv, TaskEither<Error, A>>` |
| `WithLogging()` | `withLogging()` |
| `ProductRepository` | Todo API module |
| Validation<Error, A> | Either<ValidationError[], A> |

## License

MIT
