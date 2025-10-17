export interface Todo {
  id: number;
  title: string;
  description: string | null;
  isCompleted: boolean;
  createdAt: string;
  completedAt: string | null;
}

export interface CreateTodoRequest {
  title: string;
  description: string | null;
}

export interface UpdateTodoRequest {
  title: string;
  description: string | null;
}
