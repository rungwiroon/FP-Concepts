using LanguageExt;
using LanguageExt.Common;

namespace TodoApp.Infrastructure;

public static class ApiResultHelpers
{
    public static IResult ToResult<A>(Fin<A> result, Func<A, IResult> onSuccess)
    {
        return result.Match(
            Succ: onSuccess,
            Fail: err => err.Code switch
            {
                404 => Results.NotFound(new { message = err.Message }),
                400 => Results.BadRequest(new { message = err.Message }),
                499 => Results.StatusCode(499), // Client closed request
                _ => Results.Problem(err.Message, statusCode: 500)
            }
        );
    }
}
