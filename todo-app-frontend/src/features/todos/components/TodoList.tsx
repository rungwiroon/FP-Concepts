import React from 'react';
import { useAppQuery, type RemoteData } from '../hooks';
import { listTodos, toggleTodo, deleteTodo, type Todo } from '../api';
import { useApp } from '../hooks';
import type { AppEnv } from '../../../lib/AppEnv';

interface Props {
  env: AppEnv;
  onRefresh?: () => void;
}

export const TodoList: React.FC<Props> = ({ env, onRefresh }) => {
  const { state, refetch } = useAppQuery(env, listTodos(), []);
  const { execute: executeToggle } = useApp<Todo>(env);
  const { execute: executeDelete } = useApp<void>(env);

  const handleToggle = async (id: number) => {
    await executeToggle(toggleTodo(id));
    refetch();
    onRefresh?.();
  };

  const handleDelete = async (id: number) => {
    if (window.confirm('Are you sure you want to delete this todo?')) {
      await executeDelete(deleteTodo(id));
      refetch();
      onRefresh?.();
    }
  };

  const renderContent = (state: RemoteData<Error, Todo[]>) => {
    switch (state._tag) {
      case 'NotAsked':
        return <div className="info">Not loaded yet</div>;

      case 'Loading':
        return <div className="info">Loading todos...</div>;

      case 'Failure':
        return (
          <div className="error-container">
            <p className="error">Error: {state.error.message}</p>
            <button onClick={refetch} className="btn btn-secondary">
              Retry
            </button>
          </div>
        );

      case 'Success':
        if (state.data.length === 0) {
          return (
            <div className="empty-state">
              <p>No todos yet. Create one to get started!</p>
              <button onClick={refetch} className="btn btn-secondary">
                Refresh
              </button>
            </div>
          );
        }

        return (
          <div>
            <div className="list-header">
              <h2>Your Todos</h2>
              <button onClick={refetch} className="btn btn-secondary">
                Refresh
              </button>
            </div>
            <ul className="todo-list">
              {state.data.map(todo => (
                <li key={todo.id} className={`todo-item ${todo.isCompleted ? 'completed' : ''}`}>
                  <div className="todo-content">
                    <div className="todo-info">
                      <h3>{todo.title}</h3>
                      {todo.description && <p>{todo.description}</p>}
                      <small>
                        Created: {new Date(todo.createdAt).toLocaleString()}
                        {todo.completedAt && ` | Completed: ${new Date(todo.completedAt).toLocaleString()}`}
                      </small>
                    </div>
                    <div className="todo-actions">
                      <button
                        onClick={() => handleToggle(todo.id)}
                        className={`btn ${todo.isCompleted ? 'btn-warning' : 'btn-success'}`}
                      >
                        {todo.isCompleted ? 'Undo' : 'Complete'}
                      </button>
                      <button
                        onClick={() => handleDelete(todo.id)}
                        className="btn btn-danger"
                      >
                        Delete
                      </button>
                    </div>
                  </div>
                </li>
              ))}
            </ul>
          </div>
        );
    }
  };

  return <div className="todo-list-container">{renderContent(state)}</div>;
};
