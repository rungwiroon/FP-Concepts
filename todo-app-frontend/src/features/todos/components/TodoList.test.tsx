import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { TodoList } from './TodoList';
import { createTestEnv } from '../../../test/infrastructure/TestEnv';
import type { Todo } from '../types';

describe('TodoList Component', () => {
  let testEnv: ReturnType<typeof createTestEnv>;

  beforeEach(() => {
    testEnv = createTestEnv();
  });

  it('should display loading state initially', () => {
    // Act
    render(<TodoList env={testEnv.env} />);

    // Assert
    expect(screen.getByText(/loading todos/i)).toBeInTheDocument();
  });

  it('should display todos after loading', async () => {
    // Arrange
    const todo: Todo = {
      id: 1,
      title: 'Test Todo',
      description: 'Test Description',
      isCompleted: false,
      createdAt: new Date().toISOString(),
      completedAt: null,
    };
    testEnv.httpClient.seed([todo]);

    // Act
    render(<TodoList env={testEnv.env} />);

    // Assert
    await waitFor(() => {
      expect(screen.getByText('Test Todo')).toBeInTheDocument();
    });
    expect(screen.getByText('Test Description')).toBeInTheDocument();
  });

  it('should display empty state when no todos', async () => {
    // Act
    render(<TodoList env={testEnv.env} />);

    // Assert
    await waitFor(() => {
      expect(screen.getByText(/no todos yet/i)).toBeInTheDocument();
    });
  });

  it('should display multiple todos', async () => {
    // Arrange
    const todos: Todo[] = [
      {
        id: 1,
        title: 'First Todo',
        description: null,
        isCompleted: false,
        createdAt: new Date().toISOString(),
        completedAt: null,
      },
      {
        id: 2,
        title: 'Second Todo',
        description: 'With description',
        isCompleted: true,
        createdAt: new Date().toISOString(),
        completedAt: new Date().toISOString(),
      },
    ];
    testEnv.httpClient.seed(todos);

    // Act
    render(<TodoList env={testEnv.env} />);

    // Assert
    await waitFor(() => {
      expect(screen.getByText('First Todo')).toBeInTheDocument();
      expect(screen.getByText('Second Todo')).toBeInTheDocument();
    });
  });

  it('should show completed styling for completed todos', async () => {
    // Arrange
    const todo: Todo = {
      id: 1,
      title: 'Completed Todo',
      description: null,
      isCompleted: true,
      createdAt: new Date().toISOString(),
      completedAt: new Date().toISOString(),
    };
    testEnv.httpClient.seed([todo]);

    // Act
    render(<TodoList env={testEnv.env} />);

    // Assert
    await waitFor(() => {
      const todoItem = screen.getByText('Completed Todo').closest('.todo-item');
      expect(todoItem).toHaveClass('completed');
    });
  });

  it('should toggle todo completion when Complete button clicked', async () => {
    // Arrange
    const todo: Todo = {
      id: 1,
      title: 'Test Todo',
      description: null,
      isCompleted: false,
      createdAt: new Date().toISOString(),
      completedAt: null,
    };
    testEnv.httpClient.seed([todo]);
    const user = userEvent.setup();

    // Act
    render(<TodoList env={testEnv.env} />);

    await waitFor(() => {
      expect(screen.getByText('Test Todo')).toBeInTheDocument();
    });

    const completeButton = screen.getByRole('button', { name: /complete/i });
    await user.click(completeButton);

    // Assert
    await waitFor(() => {
      const updatedTodo = testEnv.httpClient.getTodoById(1);
      expect(updatedTodo?.isCompleted).toBe(true);
      expect(updatedTodo?.completedAt).not.toBeNull();
    });

    // Should make PATCH request
    const patchCalls = testEnv.httpClient.getCallsByMethod('PATCH');
    expect(patchCalls).toHaveLength(1);
    expect(patchCalls[0].url).toBe('/todos/1/toggle');
  });

  it('should toggle completed todo back to incomplete', async () => {
    // Arrange
    const todo: Todo = {
      id: 1,
      title: 'Test Todo',
      description: null,
      isCompleted: true,
      createdAt: new Date().toISOString(),
      completedAt: new Date().toISOString(),
    };
    testEnv.httpClient.seed([todo]);
    const user = userEvent.setup();

    // Act
    render(<TodoList env={testEnv.env} />);

    await waitFor(() => {
      expect(screen.getByText('Test Todo')).toBeInTheDocument();
    });

    const undoButton = screen.getByRole('button', { name: /undo/i });
    await user.click(undoButton);

    // Assert
    await waitFor(() => {
      const updatedTodo = testEnv.httpClient.getTodoById(1);
      expect(updatedTodo?.isCompleted).toBe(false);
      expect(updatedTodo?.completedAt).toBeNull();
    });
  });

  it('should delete todo when Delete button clicked and confirmed', async () => {
    // Arrange
    const todo: Todo = {
      id: 1,
      title: 'Test Todo',
      description: null,
      isCompleted: false,
      createdAt: new Date().toISOString(),
      completedAt: null,
    };
    testEnv.httpClient.seed([todo]);
    const user = userEvent.setup();

    // Mock window.confirm to return true
    vi.stubGlobal('confirm', () => true);

    // Act
    render(<TodoList env={testEnv.env} />);

    await waitFor(() => {
      expect(screen.getByText('Test Todo')).toBeInTheDocument();
    });

    const deleteButton = screen.getByRole('button', { name: /delete/i });
    await user.click(deleteButton);

    // Assert
    await waitFor(() => {
      expect(testEnv.httpClient.getAllTodos()).toHaveLength(0);
    });

    // Should make DELETE request
    const deleteCalls = testEnv.httpClient.getCallsByMethod('DELETE');
    expect(deleteCalls).toHaveLength(1);
    expect(deleteCalls[0].url).toBe('/todos/1');

    // Cleanup
    vi.unstubAllGlobals();
  });

  it('should not delete todo when deletion is cancelled', async () => {
    // Arrange
    const todo: Todo = {
      id: 1,
      title: 'Test Todo',
      description: null,
      isCompleted: false,
      createdAt: new Date().toISOString(),
      completedAt: null,
    };
    testEnv.httpClient.seed([todo]);
    const user = userEvent.setup();

    // Mock window.confirm to return false
    vi.stubGlobal('confirm', () => false);

    // Act
    render(<TodoList env={testEnv.env} />);

    await waitFor(() => {
      expect(screen.getByText('Test Todo')).toBeInTheDocument();
    });

    const deleteButton = screen.getByRole('button', { name: /delete/i });
    await user.click(deleteButton);

    // Assert - todo should still exist
    expect(testEnv.httpClient.getAllTodos()).toHaveLength(1);

    // Should NOT make DELETE request
    const deleteCalls = testEnv.httpClient.getCallsByMethod('DELETE');
    expect(deleteCalls).toHaveLength(0);

    // Cleanup
    vi.unstubAllGlobals();
  });

  it('should refresh data when Refresh button clicked', async () => {
    // Arrange
    testEnv.httpClient.seed([]);
    const user = userEvent.setup();

    // Act
    render(<TodoList env={testEnv.env} />);

    await waitFor(() => {
      expect(screen.getByText(/no todos yet/i)).toBeInTheDocument();
    });

    // Add a todo after initial load
    const newTodo: Todo = {
      id: 1,
      title: 'New Todo',
      description: null,
      isCompleted: false,
      createdAt: new Date().toISOString(),
      completedAt: null,
    };
    testEnv.httpClient.seed([newTodo]);

    const refreshButton = screen.getByRole('button', { name: /refresh/i });
    await user.click(refreshButton);

    // Assert
    await waitFor(() => {
      expect(screen.getByText('New Todo')).toBeInTheDocument();
    });
  });

  it('should call onRefresh callback after toggle', async () => {
    // Arrange
    const todo: Todo = {
      id: 1,
      title: 'Test Todo',
      description: null,
      isCompleted: false,
      createdAt: new Date().toISOString(),
      completedAt: null,
    };
    testEnv.httpClient.seed([todo]);
    const onRefresh = vi.fn();
    const user = userEvent.setup();

    // Act
    render(<TodoList env={testEnv.env} onRefresh={onRefresh} />);

    await waitFor(() => {
      expect(screen.getByText('Test Todo')).toBeInTheDocument();
    });

    const completeButton = screen.getByRole('button', { name: /complete/i });
    await user.click(completeButton);

    // Assert
    await waitFor(() => {
      expect(onRefresh).toHaveBeenCalled();
    });
  });

  it('should call onRefresh callback after delete', async () => {
    // Arrange
    const todo: Todo = {
      id: 1,
      title: 'Test Todo',
      description: null,
      isCompleted: false,
      createdAt: new Date().toISOString(),
      completedAt: null,
    };
    testEnv.httpClient.seed([todo]);
    const onRefresh = vi.fn();
    const user = userEvent.setup();

    // Mock window.confirm to return true
    vi.stubGlobal('confirm', () => true);

    // Act
    render(<TodoList env={testEnv.env} onRefresh={onRefresh} />);

    await waitFor(() => {
      expect(screen.getByText('Test Todo')).toBeInTheDocument();
    });

    const deleteButton = screen.getByRole('button', { name: /delete/i });
    await user.click(deleteButton);

    // Assert
    await waitFor(() => {
      expect(onRefresh).toHaveBeenCalled();
    });

    // Cleanup
    vi.unstubAllGlobals();
  });

  it('should display creation and completion dates', async () => {
    // Arrange
    const createdAt = new Date('2024-01-01T10:00:00Z').toISOString();
    const completedAt = new Date('2024-01-02T15:00:00Z').toISOString();
    const todo: Todo = {
      id: 1,
      title: 'Test Todo',
      description: null,
      isCompleted: true,
      createdAt,
      completedAt,
    };
    testEnv.httpClient.seed([todo]);

    // Act
    render(<TodoList env={testEnv.env} />);

    // Assert
    await waitFor(() => {
      expect(screen.getByText(/Created:/i)).toBeInTheDocument();
      expect(screen.getByText(/Completed:/i)).toBeInTheDocument();
    });
  });
});
