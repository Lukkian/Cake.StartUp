using System;
using System.Threading;
using System.Threading.Tasks;

namespace WindowsFormsApp
{
    public class Activity<T>
    {
        private CancellationTokenSource _tokenSource;
        private Task<T> _task;
        private TimeSpan _timeout = TimeSpan.FromSeconds(10);

        public void Setup(Task<T> task, CancellationTokenSource cancellationTokenSource = null, TimeSpan timeout = default)
        {
            _tokenSource = cancellationTokenSource;
            _task = task;
            _timeout = timeout != default ? timeout : _timeout;
        }

        public async Task<T> Run()
        {
            _tokenSource = _tokenSource ?? new CancellationTokenSource();

            // Execute a long running process
            var run = _task;

            // Check the task is delaying
            if (await Task.WhenAny(run, Task.Delay(_timeout)) == run)
            {
                // task completed within the timeout
                Console.WriteLine("Task Completed Successfully");
            }
            else
            {
                // timeout
                // Cancel the task
                _tokenSource.Cancel();

                Console.WriteLine("Time Out. Aborting Task!");
            }

            // Throw error if the task was cancelled
            if (_tokenSource.IsCancellationRequested)
            {
                _tokenSource.Token.ThrowIfCancellationRequested();
            }

            // Consider that the task may have faulted or been canceled.
            // We re-await the task so that any exceptions/cancellation is rethrown.
            var result = await run;

            return result;
        }

        public Activity<T> ForTask(Task<T> task)
        {
            _task = task;
            return this;
        }

        public Activity<T> Wait(TimeSpan timeout)
        {
            _timeout = timeout;
            return this;
        }

        public Activity<T> WithToken(CancellationTokenSource token)
        {
            _tokenSource = token;
            return this;
        }
    }
}