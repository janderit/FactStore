using System;
using System.Threading;

namespace JIT.FactStore
{
    public struct AsyncResult<T>
    {
        public AsyncResult(Exception error) : this()
        {
            _error = error;
            IsError = true;
            IsAvailable = true;
        }

        public AsyncResult(T result) : this()
        {
            _result = result;
            IsAvailable = true;
        }

        private readonly Exception _error;
        private readonly T _result;

        public readonly bool IsAvailable;
        public readonly bool IsError;
        public Exception Error { get { return _error; } }
        public T Result {get
        {
            if (IsError) throw new NoResultAvailableException(typeof (T), _error);
            return _result;
        }}
    }

    public delegate void AsyncTask<T>(Action<AsyncResult<T>> on_result);

    public static class AsyncResultExtensions
    {
        public static void Result<T>(this Action<AsyncResult<T>> asyncresult, T result)
        {
            asyncresult(new AsyncResult<T>(result));
        }

        public static void Error<T>(this Action<AsyncResult<T>> asyncresult, Exception ex)
        {
            asyncresult(new AsyncResult<T>(ex));
        }

        public static T Await<T>(this AsyncTask<T> async_method, TimeSpan? timeout = null)
        {
            var result = new AsyncResult<T>();

            var wait = new ManualResetEventSlim();

            async_method(ar =>
                             {
                                 result = ar;
                                 wait.Set();
                             });

            if (!wait.Wait(timeout ?? TimeSpan.FromMilliseconds(250))) throw new TimeoutException("The data were not retrieved in time");
            if (!result.IsAvailable) throw new Exception("Internal Error");
            if (result.IsError) throw new Exception("Async Error: " + result.Error.Message, result.Error);
            return result.Result;
        }
    }
}