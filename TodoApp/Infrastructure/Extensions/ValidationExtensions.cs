using LanguageExt;
using LanguageExt.Common;
using LanguageExt.Traits;

namespace TodoApp.Infrastructure.Extensions;

/// <summary>
/// Extension methods for Validation to work with generic monads
/// </summary>
public static class ValidationExtensions
{
    /// <summary>
    /// Converts Validation<Error, A> to K<M, A> using M.Pure/M.Fail
    /// This allows validation results to be used directly in LINQ comprehensions
    /// Usage: validation.To<M, Todo>()
    /// </summary>
    public static K<M, A> To<M, A>(this Validation<Error, A> validation)
        where M : Monad<M>, Fallible<M>
    {
        return validation.Match(
            Succ: value => M.Pure(value),
            Fail: error => M.Fail<A>(error));
    }
}
