namespace TodoApp.Features.Todos;

public record CreateTodoRequest(string Title, string? Description);

public record UpdateTodoRequest(string Title, string? Description);

public record TodoResponse(
    int Id,
    string Title,
    string? Description,
    bool IsCompleted,
    DateTime CreatedAt,
    DateTime? CompletedAt
);
