import { describe, it, expect, beforeEach } from 'vitest';
import { Effect } from 'effect';
import { createTestEnv } from '../../test/infrastructure/TestEnv';
import { HttpClientService, LoggerService } from '../../lib/AppEnv';
import * as TodoAPI from './api';
import type { Todo } from './types';

describe('Todo API', () => {
  describe('listTodos', () => {
    it('should fetch all todos', async () => {
      // Arrange
      const { env, httpClient } = createTestEnv();
      const todo: Todo = {
        id: 1,
        title: 'Test Todo',
        description: 'Test Description',
        isCompleted: false,
        createdAt: new Date().toISOString(),
        completedAt: null,
      };
      httpClient.seed([todo]);

      // Act
      const result = await Effect.runPromise(
        TodoAPI.listTodos().pipe(
          Effect.provideService(HttpClientService, env.httpClient),
          Effect.provideService(LoggerService, env.logger)
        )
      );

      // Assert
      expect(result).toHaveLength(1);
      expect(result[0].title).toBe('Test Todo');
      expect(result[0].description).toBe('Test Description');
      expect(httpClient.callLog).toHaveLength(1);
      expect(httpClient.callLog[0]).toEqual({ method: 'GET', url: '/todos' });
    });

    it('should return empty array when no todos', async () => {
      // Arrange
      const { env } = createTestEnv();

      // Act
      const result = await Effect.runPromise(
        TodoAPI.listTodos().pipe(
          Effect.provideService(HttpClientService, env.httpClient),
          Effect.provideService(LoggerService, env.logger)
        )
      );

      // Assert
      expect(result).toHaveLength(0);
    });

    it('should log fetch operation', async () => {
      // Arrange
      const { env, logger } = createTestEnv();

      // Act
      await Effect.runPromise(
        TodoAPI.listTodos().pipe(
          Effect.provideService(HttpClientService, env.httpClient),
          Effect.provideService(LoggerService, env.logger)
        )
      );

      // Assert
      expect(logger.hasLog('Fetching all todos')).toBe(true);
      expect(logger.hasLog('Fetched 0 todos')).toBe(true);
    });
  });

  describe('getTodo', () => {
    it('should fetch a single todo by id', async () => {
      // Arrange
      const { env, httpClient } = createTestEnv();
      const todo: Todo = {
        id: 1,
        title: 'Test Todo',
        description: null,
        isCompleted: false,
        createdAt: new Date().toISOString(),
        completedAt: null,
      };
      httpClient.seed([todo]);

      // Act
      const result = await Effect.runPromise(
        TodoAPI.getTodo(1).pipe(
          Effect.provideService(HttpClientService, env.httpClient),
          Effect.provideService(LoggerService, env.logger)
        )
      );

      // Assert
      expect(result.id).toBe(1);
      expect(result.title).toBe('Test Todo');
      expect(httpClient.callLog[0].url).toBe('/todos/1');
    });

    it('should throw error when todo not found', async () => {
      // Arrange
      const { env } = createTestEnv();

      // Act & Assert
      await expect(
        Effect.runPromise(
          TodoAPI.getTodo(999).pipe(
            Effect.provideService(HttpClientService, env.httpClient),
            Effect.provideService(LoggerService, env.logger)
          )
        )
      ).rejects.toThrow('Todo with id 999 not found');
    });
  });

  describe('createTodo', () => {
    it('should create a new todo', async () => {
      // Arrange
      const { env, httpClient } = createTestEnv();
      const request = {
        title: 'New Todo',
        description: 'New Description',
      };

      // Act
      const result = await Effect.runPromise(
        TodoAPI.createTodo(request).pipe(
          Effect.provideService(HttpClientService, env.httpClient),
          Effect.provideService(LoggerService, env.logger)
        )
      );

      // Assert
      expect(result.id).toBe(1);
      expect(result.title).toBe('New Todo');
      expect(result.description).toBe('New Description');
      expect(result.isCompleted).toBe(false);
      expect(result.createdAt).toBeDefined();
      expect(result.completedAt).toBeNull();
      expect(httpClient.getAllTodos()).toHaveLength(1);
    });

    it('should handle null description', async () => {
      // Arrange
      const { env, httpClient } = createTestEnv();
      const request = {
        title: 'New Todo',
        description: null,
      };

      // Act
      const result = await Effect.runPromise(
        TodoAPI.createTodo(request).pipe(
          Effect.provideService(HttpClientService, env.httpClient),
          Effect.provideService(LoggerService, env.logger)
        )
      );

      // Assert
      expect(result.description).toBeNull();
    });

    it('should log create operation', async () => {
      // Arrange
      const { env, logger } = createTestEnv();
      const request = {
        title: 'New Todo',
        description: null,
      };

      // Act
      await Effect.runPromise(
        TodoAPI.createTodo(request).pipe(
          Effect.provideService(HttpClientService, env.httpClient),
          Effect.provideService(LoggerService, env.logger)
        )
      );

      // Assert
      expect(logger.hasLog('Creating todo: New Todo')).toBe(true);
      expect(logger.hasLog('Created todo with ID: 1')).toBe(true);
    });
  });

  describe('updateTodo', () => {
    it('should update an existing todo', async () => {
      // Arrange
      const { env, httpClient } = createTestEnv();
      const todo: Todo = {
        id: 1,
        title: 'Old Title',
        description: 'Old Description',
        isCompleted: false,
        createdAt: new Date().toISOString(),
        completedAt: null,
      };
      httpClient.seed([todo]);

      const request = {
        title: 'Updated Title',
        description: 'Updated Description',
      };

      // Act
      const result = await Effect.runPromise(
        TodoAPI.updateTodo(1, request).pipe(
          Effect.provideService(HttpClientService, env.httpClient),
          Effect.provideService(LoggerService, env.logger)
        )
      );

      // Assert
      expect(result.id).toBe(1);
      expect(result.title).toBe('Updated Title');
      expect(result.description).toBe('Updated Description');
    });

    it('should throw error when updating non-existent todo', async () => {
      // Arrange
      const { env } = createTestEnv();
      const request = {
        title: 'Updated Title',
        description: null,
      };

      // Act & Assert
      await expect(
        Effect.runPromise(
          TodoAPI.updateTodo(999, request).pipe(
            Effect.provideService(HttpClientService, env.httpClient),
            Effect.provideService(LoggerService, env.logger)
          )
        )
      ).rejects.toThrow('Todo with id 999 not found');
    });
  });

  describe('toggleTodo', () => {
    it('should toggle todo from incomplete to complete', async () => {
      // Arrange
      const { env, httpClient } = createTestEnv();
      const todo: Todo = {
        id: 1,
        title: 'Test Todo',
        description: null,
        isCompleted: false,
        createdAt: new Date().toISOString(),
        completedAt: null,
      };
      httpClient.seed([todo]);

      // Act
      const result = await Effect.runPromise(
        TodoAPI.toggleTodo(1).pipe(
          Effect.provideService(HttpClientService, env.httpClient),
          Effect.provideService(LoggerService, env.logger)
        )
      );

      // Assert
      expect(result.isCompleted).toBe(true);
      expect(result.completedAt).not.toBeNull();
    });

    it('should toggle todo from complete to incomplete', async () => {
      // Arrange
      const { env, httpClient } = createTestEnv();
      const todo: Todo = {
        id: 1,
        title: 'Test Todo',
        description: null,
        isCompleted: true,
        createdAt: new Date().toISOString(),
        completedAt: new Date().toISOString(),
      };
      httpClient.seed([todo]);

      // Act
      const result = await Effect.runPromise(
        TodoAPI.toggleTodo(1).pipe(
          Effect.provideService(HttpClientService, env.httpClient),
          Effect.provideService(LoggerService, env.logger)
        )
      );

      // Assert
      expect(result.isCompleted).toBe(false);
      expect(result.completedAt).toBeNull();
    });

    it('should log toggle operation', async () => {
      // Arrange
      const { env, httpClient, logger } = createTestEnv();
      const todo: Todo = {
        id: 1,
        title: 'Test Todo',
        description: null,
        isCompleted: false,
        createdAt: new Date().toISOString(),
        completedAt: null,
      };
      httpClient.seed([todo]);

      // Act
      await Effect.runPromise(
        TodoAPI.toggleTodo(1).pipe(
          Effect.provideService(HttpClientService, env.httpClient),
          Effect.provideService(LoggerService, env.logger)
        )
      );

      // Assert
      expect(logger.hasLog('Toggling todo 1')).toBe(true);
      expect(logger.hasLog('Todo 1 is now completed')).toBe(true);
    });
  });

  describe('deleteTodo', () => {
    it('should delete a todo', async () => {
      // Arrange
      const { env, httpClient } = createTestEnv();
      const todo: Todo = {
        id: 1,
        title: 'Test Todo',
        description: null,
        isCompleted: false,
        createdAt: new Date().toISOString(),
        completedAt: null,
      };
      httpClient.seed([todo]);

      // Act
      await Effect.runPromise(
        TodoAPI.deleteTodo(1).pipe(
          Effect.provideService(HttpClientService, env.httpClient),
          Effect.provideService(LoggerService, env.logger)
        )
      );

      // Assert
      expect(httpClient.getAllTodos()).toHaveLength(0);
      expect(httpClient.getTodoById(1)).toBeUndefined();
    });

    it('should throw error when deleting non-existent todo', async () => {
      // Arrange
      const { env } = createTestEnv();

      // Act & Assert
      await expect(
        Effect.runPromise(
          TodoAPI.deleteTodo(999).pipe(
            Effect.provideService(HttpClientService, env.httpClient),
            Effect.provideService(LoggerService, env.logger)
          )
        )
      ).rejects.toThrow('Todo with id 999 not found');
    });

    it('should log delete operation', async () => {
      // Arrange
      const { env, httpClient, logger } = createTestEnv();
      const todo: Todo = {
        id: 1,
        title: 'Test Todo',
        description: null,
        isCompleted: false,
        createdAt: new Date().toISOString(),
        completedAt: null,
      };
      httpClient.seed([todo]);

      // Act
      await Effect.runPromise(
        TodoAPI.deleteTodo(1).pipe(
          Effect.provideService(HttpClientService, env.httpClient),
          Effect.provideService(LoggerService, env.logger)
        )
      );

      // Assert
      expect(logger.hasLog('Deleting todo 1')).toBe(true);
      expect(logger.hasLog('Deleted todo 1')).toBe(true);
    });
  });
});
