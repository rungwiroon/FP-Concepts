# Quick Start Guide - Functional Todo App

Get the full-stack functional programming todo app running in 5 minutes!

## Prerequisites

- .NET 8 SDK installed - [Download](https://dotnet.microsoft.com/download)
- Node.js 18+ installed - [Download](https://nodejs.org/)
- A terminal/command prompt
- A web browser

## Step 1: Start the Backend (2 minutes)

### Windows (PowerShell):
```powershell
cd d:\Sourcecode\Me\fp-concepts\TodoApp
dotnet restore
dotnet run
```

### Linux/Mac (Bash):
```bash
cd /path/to/fp-concepts/TodoApp
dotnet restore
dotnet run
```

**Expected Output:**
```
Now listening on: http://localhost:5000
Application started. Press Ctrl+C to shut down.
```

✅ Backend is running on `http://localhost:5000`

## Step 2: Start the Frontend (2 minutes)

Open a **new terminal window** and run:

### Windows (PowerShell):
```powershell
cd d:\Sourcecode\Me\fp-concepts\todo-app-frontend
npm install   # Only needed first time
npm run dev
```

### Linux/Mac (Bash):
```bash
cd /path/to/fp-concepts/todo-app-frontend
npm install   # Only needed first time
npm run dev
```

**Expected Output:**
```
  VITE v5.4.20  ready in 423 ms

  ➜  Local:   http://localhost:3000/
  ➜  Network: use --host to expose
```

✅ Frontend is running on `http://localhost:3000`

## Step 3: Use the App (1 minute)

1. Open your browser to `http://localhost:3000`
2. You should see: **📝 Functional Todo App**
3. Try creating a todo:
   - Enter a title: "Learn functional programming"
   - Enter a description: "Study fp-ts and language-ext"
   - Click "Create Todo"
4. Your todo should appear in the list below!

## Quick Test Script

Want to test the API directly? Open another terminal:

### Windows (PowerShell):
```powershell
cd d:\Sourcecode\Me\fp-concepts\TodoApp
.\test-api.ps1
```

### Linux/Mac (Bash):
```bash
cd /path/to/fp-concepts/TodoApp
chmod +x test-api.sh
./test-api.sh
```

This will create, list, update, toggle, and delete todos via the API.

## Troubleshooting

### CORS Error

**Error: Response body is not available to scripts (Reason: CORS Missing Allow Origin)**

The backend has been configured with CORS support. If you still see this error:

1. **Restart the backend** - Make sure you restart after the CORS fix:
   ```bash
   cd TodoApp
   dotnet run
   ```

2. **Check the port** - Frontend should be on port 3000 or 3001 (both are allowed)

3. **Clear browser cache** - Press Ctrl+Shift+R to hard refresh

✅ See [CORS-FIX.md](CORS-FIX.md) for detailed information

### Backend won't start

**Error: Port 5000 already in use**
```bash
# Find and kill the process using port 5000
# Windows:
netstat -ano | findstr :5000
taskkill /PID <PID> /F

# Linux/Mac:
lsof -ti:5000 | xargs kill -9
```

**Error: Database locked**
```bash
# Delete the database file and restart
cd TodoApp
rm todos.db
dotnet run
```

### Frontend won't start

**Error: Port 3000 already in use**

Edit `todo-app-frontend/vite.config.ts` and change the port:
```typescript
export default defineConfig({
  plugins: [react()],
  server: {
    port: 3001,  // Change to 3001
  },
})
```

**Error: npm install fails**

Try clearing the cache:
```bash
npm cache clean --force
rm -rf node_modules package-lock.json
npm install
```

### Frontend can't connect to backend

**Error: Failed to fetch**

1. Make sure backend is running on `http://localhost:5000`
2. Check `todo-app-frontend/src/App.tsx` has correct URL:
   ```typescript
   const env: AppEnv = {
     httpClient: createHttpClient('http://localhost:5000'),
     // ...
   };
   ```

## Features to Try

### ✅ Create Todos
- Enter title and description
- See validation errors for invalid input
- Watch the console for functional logging

### ✅ Complete Todos
- Click "Complete" button
- Todo gets strike-through
- CompletedAt timestamp is set

### ✅ Delete Todos
- Click "Delete" button
- Confirm deletion
- Todo is removed

### ✅ Validation
Try these to see error accumulation:
- Leave title empty
- Enter a description over 1000 characters
- Multiple errors show at once!

## What to Observe

### Console Logs (Browser DevTools)

Open browser DevTools (F12) and check the Console tab:

```
[INFO] Fetching all todos
[INFO] Fetched 0 todos
[INFO] Creating todo: Learn functional programming
[INFO] Created todo with ID: 1
[INFO] Fetching all todos
[INFO] Fetched 1 todos
```

These logs come from the functional **logging effect** composition!

### Backend Logs (Terminal)

Check the backend terminal:

```
info: TodoApp.Program[0]
      Metrics - ListTodos: 45.2ms
info: TodoApp.Program[0]
      Retrieved 1 todos
```

These logs come from **WithLogging** and **WithMetrics** effect extensions!

## Next Steps

### Explore the Code

#### Backend Functional Patterns
```
TodoApp/Infrastructure/DbMonad.cs       # The Db monad
TodoApp/Features/Todos/TodoRepository.cs # Repository with FP patterns
TodoApp/Infrastructure/Extensions/      # Effect extensions
```

#### Frontend Functional Patterns
```
todo-app-frontend/src/lib/AppMonad.ts                # The App monad
todo-app-frontend/src/features/todos/api.ts          # API with fp-ts
todo-app-frontend/src/features/todos/hooks.ts        # RemoteData pattern
todo-app-frontend/src/features/todos/validation.ts   # Applicative validation
```

### Read the Guides

- [functional-backend-guide.md](functional-guide.md) - Backend FP patterns
- [functional-frontend-guide.md](functional-frontend-guide.md) - Frontend FP patterns
- [FULLSTACK-SUMMARY.md](FULLSTACK-SUMMARY.md) - Complete overview

### Extend the App

Try adding:
- **Backend**: Cache extension, transaction support
- **Frontend**: Retry effect, optimistic updates
- **Both**: Todo categories, due dates, search

## Summary

You now have a fully functional full-stack application demonstrating:

✅ **Backend**: language-ext with Db monad, LINQ query syntax, effect extensions <br/>
✅ **Frontend**: fp-ts with App monad, pipe composition, RemoteData pattern <br/>
✅ **Full Stack**: Type-safe, composable, testable, maintainable code! <br/>

## Need Help?

- Check [FULLSTACK-SUMMARY.md](FULLSTACK-SUMMARY.md) for architecture details
- Review the individual READMEs in `TodoApp/` and `todo-app-frontend/`
- Examine the functional guides for pattern explanations

Happy functional programming! 🎉
