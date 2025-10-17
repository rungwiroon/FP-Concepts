using LanguageExt;
using LanguageExt.Common;
using LanguageExt.Traits;
using TodoApp.Domain;
using static LanguageExt.Prelude;

namespace TodoApp.Features.Todos;

public static class TodoValidation
{
    public static Validation<Error, Todo> Validate(Todo todo) =>
        (ValidateTitle(todo), ValidateDescription(todo))
            .Apply((_, _) => todo).As();

    private static Validation<Error, Todo> ValidateTitle(Todo todo) =>
        !string.IsNullOrWhiteSpace(todo.Title) && todo.Title.Length <= 200
            ? Success<Error, Todo>(todo)
            : Fail<Error, Todo>(Error.New("Title is required and must be less than 200 characters"));

    private static Validation<Error, Todo> ValidateDescription(Todo todo) =>
        todo.Description == null || todo.Description.Length <= 1000
            ? Success<Error, Todo>(todo)
            : Fail<Error, Todo>(Error.New("Description must be less than 1000 characters"));
}
