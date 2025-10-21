import React, { useState } from 'react';
import { createHttpClient } from './lib/HttpClient';
import type { AppEnv } from './lib/AppEnv';
import { TodoList } from './features/todos/components/TodoList';
import { TodoForm } from './features/todos/components/TodoForm';
import './App.css';

// Create the environment
const env: AppEnv = {
  httpClient: createHttpClient('http://localhost:5000'),
  logger: {
    info: (message) => console.info('[INFO]', message),
    warn: (message) => console.warn('[WARN]', message),
    error: (message, err) => console.error('[ERROR]', message, err),
  },
  baseUrl: 'http://localhost:5000',
};

export const App: React.FC = () => {
  const [refreshKey, setRefreshKey] = useState(0);

  const handleSuccess = () => {
    // Trigger refresh of the list
    setRefreshKey(prev => prev + 1);
  };

  return (
    <div className="app">
      <header className="app-header">
        <h1>📝 Functional Todo App</h1>
        <p className="subtitle">Built with Effect-TS & React</p>
      </header>

      <div className="container">
        <section className="section">
          <h2>Create New Todo</h2>
          <TodoForm env={env} onSuccess={handleSuccess} />
        </section>

        <section className="section">
          <TodoList key={refreshKey} env={env} onRefresh={handleSuccess} />
        </section>
      </div>

      <footer className="app-footer">
        <p>
          Functional Programming with <strong>Effect-TS</strong> |
          Backend: ASP.NET Core with <strong>language-ext</strong>
        </p>
      </footer>
    </div>
  );
};

export default App;
