using System;
using System.Threading;
using System.Threading.Tasks;

namespace WindowsFormsApp
{
    public class Activity<T>
    {
        private CancellationTokenSource _tokenSource;
        private Task<T> _task;
        private const int Defaulttimeout = 1000;
        private int _timeout = Defaulttimeout;

        public void Setup(Task<T> task, int timeout = Defaulttimeout)
        {
            _task = task;
            _timeout = timeout;
        }

        public async Task<T> Run()
        {
            _tokenSource = new CancellationTokenSource();
            var run = Task.Run(() => _task, _tokenSource.Token); // Execute a long running process

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

                Console.WriteLine("Time Out. Aborting Task");

                // throw OperationCanceledException
                _tokenSource.Token.ThrowIfCancellationRequested();
            }

            // Consider that the task may have faulted or been canceled.
            // We re-await the task so that any exceptions/cancellation is rethrown.
            return await run;
        }

        public Activity<T> ForTask(Task<T> task)
        {
            _task = task;
            return this;
        }

        public Activity<T> Wait(int timeout)
        {
            _timeout = timeout;
            return this;
        }
    }
}