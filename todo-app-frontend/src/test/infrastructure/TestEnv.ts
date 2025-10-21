import type { AppEnv } from '../../lib/AppEnv';
import { TestHttpClient } from './TestHttpClient';
import { TestLogger } from './TestLogger';
import type { Todo } from '../../features/todos/types';

/**
 * Test Environment Factory
 * Similar to TestRuntime in backend - provides test implementations of all traits
 * Creates isolated test environment for each test
 */
export const createTestEnv = (initialTodos: Todo[] = []): {
  env: AppEnv;
  httpClient: TestHttpClient;
  logger: TestLogger;
} => {
  const httpClient = new TestHttpClient(initialTodos);
  const logger = new TestLogger();

  const env: AppEnv = {
    httpClient,
    logger,
    baseUrl: 'http://test',
  };

  return { env, httpClient, logger };
};
