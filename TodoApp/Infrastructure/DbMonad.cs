using LanguageExt;
using LanguageExt.Common;
using LanguageExt.Traits;
using Microsoft.EntityFrameworkCore;
using static LanguageExt.Prelude;

namespace TodoApp.Infrastructure;

public record Db<A>(ReaderT<DbEnv, IO, A> RunDb) : K<Db, A>
{
    public Db<B> Select<B>(Func<A, B> m) =>
        this.Kind().Select(m).As();

    public Db<C> SelectMany<B, C>(Func<A, K<Db, B>> bind, Func<A, B, C> project) =>
        this.Kind().SelectMany(bind, project).As();

    public async Task<Fin<A>> Run(DbEnv env) =>
        await RunDb.Run(env).RunAsync();
}

public abstract partial class Db : Monad<Db>, Readable<Db, DbEnv>, Fallible<Error, Db>
{
    public static K<Db, B> Map<A, B>(Func<A, B> f, K<Db, A> ma) =>
        new Db<B>(ma.As().RunDb.Map(f).As());

    public static K<Db, A> Pure<A>(A value) =>
        new Db<A>(ReaderT<DbEnv, IO, A>.Pure(value));

    public static K<Db, B> Bind<A, B>(K<Db, A> ma, Func<A, K<Db, B>> f) =>
        new Db<B>(ma.As().RunDb.Bind(x => f(x).As().RunDb).As());

    public static K<Db, B> Apply<A, B>(K<Db, Func<A, B>> mf, K<Db, A> ma) =>
        mf.Bind(f => ma.Map(f));

    public static K<Db, A> Fail<A>(Error error) =>
        new Db<A>(ReaderT.liftIO<DbEnv, IO, A>(IO<A>.Fail(error)));

    public static K<Db, A> Catch<A>(
        K<Db, A> ma,
        Func<Error, bool> predicate,
        Func<Error, K<Db, A>> handler) =>
        ma.As(); // Simplified for now - can be enhanced later

    // Lifting operations
    public static K<Db, A> LiftIO<A>(Task<A> task) =>
        new Db<A>(ReaderT.liftIO<DbEnv, IO, A>(
            IO.liftAsync(async _ => await task.ConfigureAwait(false))));

    public static K<Db, A> LiftIO<A>(Func<Task<A>> task) =>
        new Db<A>(ReaderT.liftIO<DbEnv, IO, A>(
            IO.liftAsync(async _ => await task().ConfigureAwait(false))));

    public static K<Db, A> Lift<A>(Validation<Error, A> validation) =>
        new Db<A>(ReaderT<DbEnv, IO, A>.Lift(
            validation.Match(
                Succ: IO<A>.Pure,
                Fail: IO<A>.Fail)));

    // Readable interface methods
    public static K<Db, DbEnv> Ask() =>
        new Db<DbEnv>(ReaderT<DbEnv, IO, DbEnv>.Asks(Prelude.identity));

    public static K<Db, A> Asks<A>(Func<DbEnv, A> f) =>
        Ask().Map(f);

    public static K<Db, A> Local<A>(Func<DbEnv, DbEnv> f, K<Db, A> ma) =>
        new Db<A>(ma.As().RunDb.Local(f).As());

    // Accessing dependencies
    public static Db<TContext> Ctx<TContext>() where TContext : DbContext =>
        Ask().As().Select(env => (env.DbContext as TContext)!);

    public static Db<CancellationToken> CancellationToken() =>
        Ask().As().Select(env => env.CancellationToken);

    public static Db<ILogger> Logger() =>
        Ask().As().Select(env => env.Logger);

    public static Db<Unit> Save() =>
        from env in Ask().As()
        from _ in LiftIO(() => env.DbContext.SaveChangesAsync(env.CancellationToken))
        select unit;
}

public static class DbExtensions
{
    public static Db<A> As<A>(this K<Db, A> ma) =>
        (Db<A>)ma;

    public static ReaderT<DbEnv, IO, A> Run<A>(this K<Db, A> ma) =>
        ma.As().RunDb;

    public static async Task<Fin<A>> RunAsync<A>(this K<Db, A> ma, DbEnv env) =>
        await ma.Run().Run(env).RunAsync();
}
