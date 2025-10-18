using LanguageExt;
using LanguageExt.Common;
using LanguageExt.Traits;

namespace TodoApp.Infrastructure.Extensions;

/// <summary>
/// Extension methods for Option to work with generic monads
/// </summary>
public static class OptionExtensions
{
    /// <summary>
    /// Converts Option<A> to K<M, A> using M.Pure/M.Fail
    /// This allows option results to be used directly in LINQ comprehensions
    /// Usage: option.To<M, Todo>(Error.New(404, "Not found"))
    /// </summary>
    public static K<M, A> To<M, A>(this Option<A> option, Error error)
        where M : Monad<M>, Fallible<M>
    {
        return option.Match(
            Some: value => M.Pure(value),
            None: () => M.Fail<A>(error));
    }

    /// <summary>
    /// Converts Option<A> to K<M, A> using M.Pure/M.Fail with error factory
    /// This allows option results to be used directly in LINQ comprehensions
    /// Usage: option.To<M, Todo>(() => Error.New(404, $"Todo {id} not found"))
    /// </summary>
    public static K<M, A> To<M, A>(this Option<A> option, Func<Error> errorFactory)
        where M : Monad<M>, Fallible<M>
    {
        return option.Match(
            Some: value => M.Pure(value),
            None: () => M.Fail<A>(errorFactory()));
    }
}
