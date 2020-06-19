using System;
using System.Threading.Tasks;

namespace PlainHttp
{
    static class AsyncExtensionMethods
    {
        public static async Task<TResult> WithTimeout<TResult>(this Task<TResult> task, TimeSpan timeout)
        {
            if (timeout == default)
            {
                return await task;
            }

            if (task == await Task.WhenAny(task, Task.Delay(timeout)))
            {
                return await task;
            }

            throw new OperationCanceledException("Timeout occured while reading");
        }
    }
}
