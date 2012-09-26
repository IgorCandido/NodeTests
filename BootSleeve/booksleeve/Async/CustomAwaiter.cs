using System;
using System.Threading;
using System.Diagnostics;
using System.Threading.Tasks;

// the classes in this file are intended to illustrate a bespoke awaiter implementation - nothing more, nothing less
namespace Async
{
    class MonitorPool
    {
        private readonly object[] pool;
        public MonitorPool(int length)
        {
            this.pool = new object[length];
        }
        public object Get()
        {
            object obj;
            for (int i = 0; i < pool.Length; i++)
            {
                if ((obj = Interlocked.Exchange(ref pool[i], null)) != null) return obj; // found something useful
            }
            return new object();
        }
        public void Return(object obj)
        {
            if (obj != null)
            { 
                for (int i = 0; i < pool.Length; i++)
                {
                    if (Interlocked.CompareExchange(ref pool[i], obj, null) == null) return; // found an empty slot
                }
            }
            // just drop it on the floor
        }
    }

    class CustomAwaiter
    {
        public static void Execute()
        {
#pragma warning disable 0618
            Future<int>[] futures = new Future<int>[10];
            MonitorPool pool = new MonitorPool(futures.Length);
            const int loop = 500000;
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
            GC.WaitForPendingFinalizers();
            var watch = Stopwatch.StartNew();
            for (int j = 0; j < loop; j++)
            {                 
                for (int i = 0; i < futures.Length; i++)
                {
                    futures[i] = new Future<int>(syncLock: pool.Get());
                }
                for (int i = 0; i < futures.Length; i++)
                {
                    pool.Return(futures[i].OnSuccess(i+1));
                }
                for (int i = 0; i < futures.Length; i++)
                {
                    if (futures[i].Result != i+1) throw new InvalidOperationException();
                }
            }
            watch.Stop();

            Console.WriteLine("Future (uncontested): " + watch.ElapsedMilliseconds);
#pragma warning restore 0618
            Task<int>[] tasks = new Task<int>[futures.Length];
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
            GC.WaitForPendingFinalizers();
            watch = Stopwatch.StartNew();
            Func<object, int> selector = state => (int)state;
            for (int j = 0; j < loop; j++)
            {
                for (int i = 0; i < futures.Length; i++)
                {
                    tasks[i] = new Task<int>(selector, i+1);
                }
                for (int i = 0; i < futures.Length; i++)
                {
                    tasks[i].RunSynchronously(TaskScheduler.Default);
                }
                for (int i = 0; i < futures.Length; i++)
                {
                    if (tasks[i].Result != i+1) throw new InvalidOperationException();
                }
            }
            watch.Stop();

            Console.WriteLine("Task (uncontested): " + watch.ElapsedMilliseconds);
            TaskCompletionSource<int>[] sources = new TaskCompletionSource<int>[futures.Length];
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
            GC.WaitForPendingFinalizers();
            watch = Stopwatch.StartNew();
            for (int j = 0; j < loop; j++)
            {
                for (int i = 0; i < futures.Length; i++)
                {
                    sources[i] = new TaskCompletionSource<int>();
                }
                for (int i = 0; i < futures.Length; i++)
                {
                    sources[i].SetResult(i+1);
                }
                for (int i = 0; i < futures.Length; i++)
                {
                    if (sources[i].Task.Result != i + 1) throw new InvalidOperationException();
                }
            }
            watch.Stop();

            Console.WriteLine("Source (uncontested): " + watch.ElapsedMilliseconds);
#pragma warning disable 0618
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
            GC.WaitForPendingFinalizers();
            watch = Stopwatch.StartNew();
            for (int j = 0; j < loop; j++)
            {
                for (int i = 0; i < futures.Length; i++)
                {
                    futures[i] = new Future<int>(syncLock: pool.Get());
                }
                ThreadPool.QueueUserWorkItem(delegate
                {
                    for (int i = 0; i < futures.Length; i++)
                    {
                        pool.Return(futures[i].OnSuccess(i+1));
                    }
                });
                for (int i = 0; i < futures.Length; i++)
                {
                    int x = futures[i].Result;
                    if (x != i+1)
                    {
                        throw new InvalidOperationException(string.Format("{0} instead of {1}", x, i+1));
                    }
                }
            }
            watch.Stop();
#pragma warning restore 0618
            Console.WriteLine("Future (contested): " + watch.ElapsedMilliseconds);

            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
            GC.WaitForPendingFinalizers();
            watch = Stopwatch.StartNew();
            for (int j = 0; j < loop; j++)
            {
                for (int i = 0; i < futures.Length; i++)
                {
                    tasks[i] = new Task<int>(selector, i+1);
                }
                ThreadPool.QueueUserWorkItem(delegate
                {
                    for (int i = 0; i < futures.Length; i++)
                    {
                        tasks[i].RunSynchronously(TaskScheduler.Default);
                    }
                });
                for (int i = 0; i < futures.Length; i++)
                {
                    if (tasks[i].Result != i+1) throw new InvalidOperationException();
                }
            }
            watch.Stop();

            Console.WriteLine("Task (contested): " + watch.ElapsedMilliseconds);


            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
            GC.WaitForPendingFinalizers();
            watch = Stopwatch.StartNew();
            for (int j = 0; j < loop; j++)
            {
                for (int i = 0; i < futures.Length; i++)
                {
                    sources[i] = new TaskCompletionSource<int>();
                }
                ThreadPool.QueueUserWorkItem(delegate
                {
                    for (int i = 0; i < futures.Length; i++)
                    {
                        sources[i].SetResult(i+1);
                    }
                });
                for (int i = 0; i < futures.Length; i++)
                {
                    if (sources[i].Task.Result != i + 1) throw new InvalidOperationException();
                }
            }
            watch.Stop();

            Console.WriteLine("Source (contested): " + watch.ElapsedMilliseconds);
        }


        static void Main2()
        {
#pragma warning disable 0618
            var typed = new Future<int>();
            Future untyped = typed;
#pragma warning restore 0618
            DoSomet(typed);
            DoSomet(untyped);
            ThreadPool.QueueUserWorkItem(delegate
            {
                Thread.Sleep(5000);
                Console.WriteLine("setting...");
                object gotBack = typed.OnSuccess(123);
                Console.WriteLine("set; " + (gotBack == null ? "nothing" : "syncLock"));
            });
            untyped.Wait();
            int i = typed.Result;
            Console.WriteLine("sync: " + i);
            Console.ReadLine();
        }
#pragma warning disable 0618
        static async void DoSomet(Future obj)
        {

            await obj;
            Console.WriteLine("async");
        }
        static async void DoSomet(Future<int> obj)
        {
            var i = await obj;
            Console.WriteLine("async: " + i);
        }
#pragma warning restore 0618
    }

    public interface IFutureAwaiter
    {
        /// <summary>
        /// Has the operation completed?
        /// </summary>
        bool IsCompleted { get; }
        /// <summary>
        /// Register an operation to perform when the operation has comleted; if it has *already*
        /// completed this operation is performed immediately.
        /// </summary>
        void OnCompleted(Action callback);
        /// <summary>
        /// Assert successful completion of the operation; if the operation completed with error, the exception
        /// is propegated. If the operation has not yet completed this will block.
        /// </summary>
        void GetResult();
    }
    public interface IFutureAwaiter<T>
    {
        /// <summary>
        /// Has the operation completed?
        /// </summary>
        bool IsCompleted { get; }
        /// <summary>
        /// Register an operation to perform when the operation has comleted; if it has *already*
        /// completed this operation is performed immediately.
        /// </summary>
        void OnCompleted(Action callback);
        /// <summary>
        /// Assert successful completion of the operation; if the operation completed with error, the exception
        /// is propegated; otherwise the specified result is returned. If no result was specified then
        /// the default value of T is returned.
        /// If the operation has not yet completed this will block.
        /// </summary>
        T GetResult();
    }


    /// <summary>
    /// Represents an asynchronous operation without a return value; this may have completed already, or may complete at any time.
    /// Both synchronous (blocking) operations and asynchronous (continuation) operations are supported.
    /// </summary>
    /// <remarks>All members on this class are thread-safe.</remarks>
    [Obsolete("DO NOT USE THIS; TaskCompletionSource<T> is preferred")]
    public class Future : IFutureAwaiter
    {
        /// <summary>
        /// Create a new Future object
        /// </summary>
        /// <param name="millisecondsTimeout">The time to wait in GetResult beore a TimeoutException is thrown</param>
        /// <param name="syncLock">The object to use as for use with Monitor; if none is supplied a new object
        /// is created for the purpose.</param>
        public Future(int millisecondsTimeout = int.MaxValue, object syncLock = null)
        {
            this.syncLock = syncLock ?? new object();
            this.millisecondsTimeout = millisecondsTimeout;
        }
        private readonly int millisecondsTimeout;
        private Exception exception;
        private object syncLock;
        /// <summary>
        /// Returns an "awaiter" for use with async/await
        /// </summary>
        public IFutureAwaiter GetAwaiter() { return this;}
        private Action callbacks;
        /// <summary>
        /// Has the operation completed?
        /// </summary>
        public bool IsCompleted {
            get {
                return syncLock == null;
                // Interlocked would be more accurate; but the flip is atomic, at least, so
                // we'll accept the *unlikely* chance of a misread, and absorb that into OnCompleted
            }
        }
        
        void IFutureAwaiter.OnCompleted(Action callback)
        {
            if (callback == null) return; // nothing to do
            object localLock;
            
            if (syncLock == null || (localLock = Interlocked.CompareExchange(ref syncLock, null, null)) == null)
            {
                callback(); // has already completed
                return;
            }

            lock (localLock)
            {
                if (syncLock == null)
                {
                    // completed *just now*
                    callback();
                    return;
                }

                // enqueue that work
                callbacks += callback;
            }
        }

        /// <summary>
        /// Assert successful completion of the operation; if the operation completed with error, the exception
        /// is propegated. If the operation has not yet completed this will block.
        /// </summary>
        public void Wait()
        {
            object localLock;
            if (syncLock == null || (localLock = Interlocked.CompareExchange(ref syncLock, null, null)) == null)
            {
                // already completed
                Thread.MemoryBarrier();
                if (exception != null) throw exception;
                return;
            }

            bool completed;
            lock (localLock)
            {
                completed = syncLock == null; // true if completed *just now*

                if (!completed)
                {
                    completed = Monitor.Wait(localLock, millisecondsTimeout);
                }
            }
            if (completed)
            {
                // completed while we were waiting
                if (exception != null) throw exception;
            }
            else
            {
                throw new TimeoutException(); // took too long
            }
        }
        void IFutureAwaiter.GetResult()
        {
            Wait();
        }
        /// <summary>
        /// Signals that the operation has completed successfully.
        /// </summary>
        /// <returns>The object being used for Monitor locking is returned, and should
        /// now (if non-null) be avaialble for re-use if desired</returns>
        public object OnSuccess()
        {
            int tmp = 0;
            return OnComplete(ref tmp, 0);
        } 
        /// <summary>
        /// Signals that the operation has completed with error
        /// </summary>
        /// <param name="exception">The exception encountered</param>
        /// <returns>The object being used for Monitor locking is returned, and should
        /// now (if non-null) be avaialble for re-use if desired</returns>
        public object OnFailure(Exception exception)
        {
            if (exception == null) throw new ArgumentNullException("exception");
            return OnComplete(ref this.exception, exception);
        }
        /// <summary>
        /// Signals that the operation has completed, allowing auxiliary data to be set
        /// </summary>
        /// <typeparam name="T">The type of auxiliary data to set</typeparam>
        /// <param name="field">The auxiliary field to assign (after checks and inside suitable synchronization, etc)</param>
        /// <param name="value">The new value to assign to the auxiliary field</param>
        /// <returns>The object being used for Monitor locking is returned, and should
        /// now (if non-null) be avaialble for re-use if desired</returns>
        protected object OnComplete<T>(ref T field, T value)
        {
            object localLock = Interlocked.CompareExchange(ref syncLock, null, null);
            if (localLock == null)
            {
                throw new InvalidOperationException("The Future is already completed"); 
            }
            Action callbacks;
            lock (localLock)
            {
                // need to use Interlocked here, since that is what the other readers are using
                if (Interlocked.CompareExchange(ref syncLock, null, null) == null)
                {
                    throw new InvalidOperationException("The Future is already completed");
                }
                field = value;
                Interlocked.Exchange(ref syncLock, null);
                
                Monitor.PulseAll(localLock); // open up for sync waiters

                callbacks = this.callbacks;
                this.callbacks = null; // release for GC
            }
            if (callbacks != null)
            {   // invoke async waiters
                foreach (Action action in callbacks.GetInvocationList())
                {
                    try
                    {
                        action();
                    }
                    catch (Exception ex)
                    {
                        var handler = CallbackException;
                        if (handler != null) { handler(ex); }
                    }
                }
            }
            return localLock;
        }
        /// <summary>
        /// Global event that signals exceptions encountered signalling callbacks
        /// </summary>
        public static event Action<Exception> CallbackException;
    }
    /// <summary>
    /// Represents an asynchronous operation with a return value; this may have completed already, or may complete at any time.
    /// Both synchronous (blocking) operations and asynchronous (continuation) operations are supported.
    /// </summary>
    /// <remarks>All members on this class are thread-safe.</remarks>
    [Obsolete("DO NOT USE THIS; TaskCompletionSource<T> is preferred")]
#pragma warning disable 0618
    public sealed class Future<T> : Future, IFutureAwaiter<T>
    {
#pragma warning restore 0618
        /// <summary>
        /// Create a new Future object
        /// </summary>
        /// <param name="millisecondsTimeout">The time to wait in GetResult beore a TimeoutException is thrown</param>
        /// <param name="syncLock">The object to use as for use with Monitor; if none is supplied a new object
        /// is created for the purpose.</param>
        public Future(int millisecondsTimeout = int.MaxValue, object syncLock = null) : base(millisecondsTimeout, syncLock)
        { }

        /// <summary>
        /// Returns an "awaiter" for use with async/await
        /// </summary>
        public new IFutureAwaiter<T> GetAwaiter() { return this;}
        private T result;

        void IFutureAwaiter<T>.OnCompleted(Action callback)
        {
            ((IFutureAwaiter)this).OnCompleted(callback);
        }

        T IFutureAwaiter<T>.GetResult()
        {
            return Result;
        }
        /// <summary>
        /// Assert successful completion of the operation; if the operation completed with error, the exception
        /// is propegated; otherwise the specified result is returned. If no result was specified then
        /// the default value of T is returned.
        /// If the operation has not yet completed this will block.
        /// </summary>
        public T Result
        {
            get
            {
                Wait();
                return result;
            }
        }

        /// <summary>
        /// Signals that the operation has completed successfully, assigning a result value.
        /// </summary>
        /// <returns>The object being used for Monitor locking is returned, and should
        /// now (if non-null) be avaialble for re-use if desired</returns>        
        public object OnSuccess(T result)
        {
            return OnComplete(ref this.result, result);
        }
    }
}
