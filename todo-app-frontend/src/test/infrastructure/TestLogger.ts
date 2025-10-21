import type { Logger } from '../../lib/AppEnv';

/**
 * Test Logger - In-memory implementation
 * Captures all log messages for testing assertions
 * Similar to TestLoggerIO in backend
 */
export class TestLogger implements Logger {
  public logs: Array<{ level: string; message: string; error?: unknown }> = [];

  info = (message: string) => {
    this.logs.push({ level: 'info', message });
  };

  warn = (message: string) => {
    this.logs.push({ level: 'warn', message });
  };

  error = (message: string, err?: unknown) => {
    this.logs.push({ level: 'error', message, error: err });
  };

  // ========== Test Helper Methods ==========

  /**
   * Clear all logs
   */
  clear() {
    this.logs = [];
  }

  /**
   * Get all info-level logs
   */
  getInfoLogs(): string[] {
    return this.logs.filter(l => l.level === 'info').map(l => l.message);
  }

  /**
   * Get all error-level logs
   */
  getErrorLogs(): string[] {
    return this.logs.filter(l => l.level === 'error').map(l => l.message);
  }

  /**
   * Get all warning-level logs
   */
  getWarnLogs(): string[] {
    return this.logs.filter(l => l.level === 'warn').map(l => l.message);
  }

  /**
   * Check if a log message contains specific text
   */
  hasLog(message: string): boolean {
    return this.logs.some(l => l.message.includes(message));
  }

  /**
   * Get the count of logs at a specific level
   */
  getLogCount(level?: string): number {
    if (!level) return this.logs.length;
    return this.logs.filter(l => l.level === level).length;
  }
}
