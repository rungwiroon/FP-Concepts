import React, { useState } from 'react';
import { pipe } from 'fp-ts/function';
import * as E from 'fp-ts/Either';
import { useApp } from '../hooks';
import { createTodo, type CreateTodoRequest } from '../api';
import { validateTodo, type ValidationError } from '../validation';
import type { AppEnv } from '../../../lib/AppEnv';
import type { Todo } from '../types';

interface Props {
  env: AppEnv;
  onSuccess?: () => void;
}

export const TodoForm: React.FC<Props> = ({ env, onSuccess }) => {
  const [formData, setFormData] = useState<CreateTodoRequest>({
    title: '',
    description: null,
  });
  const [validationErrors, setValidationErrors] = useState<ValidationError[]>([]);
  const { state, execute } = useApp<Todo>(env);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    // Validate
    const validationResult = validateTodo(formData);

    pipe(
      validationResult,
      E.fold(
        // Validation failed
        (errors) => {
          setValidationErrors(errors);
        },
        // Validation passed
        async (validData) => {
          setValidationErrors([]);

          // Execute the API call
          await execute(createTodo(validData));

          if (state._tag === 'Success' || state._tag === 'NotAsked') {
            setFormData({ title: '', description: null });
            onSuccess?.();
          }
        }
      )
    );
  };

  const getFieldError = (field: string) =>
    validationErrors.find(e => e.field === field)?.message;

  return (
    <form onSubmit={handleSubmit} className="todo-form">
      <div className="form-group">
        <label htmlFor="title">
          Title *
        </label>
        <input
          id="title"
          type="text"
          value={formData.title}
          onChange={(e) => setFormData({ ...formData, title: e.target.value })}
          className={getFieldError('title') ? 'error-input' : ''}
          placeholder="Enter todo title"
        />
        {getFieldError('title') && (
          <span className="error-message">{getFieldError('title')}</span>
        )}
      </div>

      <div className="form-group">
        <label htmlFor="description">
          Description
        </label>
        <textarea
          id="description"
          value={formData.description || ''}
          onChange={(e) => setFormData({ ...formData, description: e.target.value || null })}
          className={getFieldError('description') ? 'error-input' : ''}
          placeholder="Enter todo description (optional)"
          rows={3}
        />
        {getFieldError('description') && (
          <span className="error-message">{getFieldError('description')}</span>
        )}
      </div>

      <button type="submit" disabled={state._tag === 'Loading'} className="btn btn-primary">
        {state._tag === 'Loading' ? 'Creating...' : 'Create Todo'}
      </button>

      {state._tag === 'Failure' && (
        <div className="error-message">Error: {state.error.message}</div>
      )}

      {state._tag === 'Success' && (
        <div className="success-message">Todo created successfully!</div>
      )}
    </form>
  );
};
