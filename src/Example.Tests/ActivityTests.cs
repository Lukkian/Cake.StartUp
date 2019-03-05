using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using WindowsFormsApp;

namespace Example.Tests
{
    [TestFixture]
    public sealed class ActivityTests
    {
        [Test]
        public async Task Can_run_a_Task_within_a_time_limit()
        {
            // Given
            var activity = new Activity<int>();
            var task = Add(9);
            activity.Setup(task, null, TimeSpan.FromMilliseconds(600));

            // When
            var count = await activity.Run();

            // Then
            Assert.That(count, Is.EqualTo(10));
        }

        [Test]
        public async Task Can_run_a_Task_within_a_time_limit_fluent_mode()
        {
            // Given
            var activity = new Activity<int>();
            var token = new CancellationTokenSource();
            var task = Add(9, token);

            // When
            var count = await activity.ForTask(task).WithToken(token).Wait(TimeSpan.FromMilliseconds(600)).Run();

            // Then
            Assert.That(count, Is.EqualTo(10));
        }

        [Test]
        public async Task Throw_error_when_timeout()
        {
            // Given
            var activity = new Activity<int>();
            var task = Add(9);
            activity.Setup(task, null, TimeSpan.FromMilliseconds(100));

            var count = 0;
            Exception ex = null;

            // When
            try
            {
                count = await activity.Run();
            }
            catch (Exception e)
            {
                ex = e;
            }

            // Then
            Assert.That(ex, Is.TypeOf<OperationCanceledException>());
            Assert.That(count, Is.EqualTo(0));
        }

        [Test]
        public async Task Throw_error_when_timeout_with_shared_token()
        {
            // Given
            var activity = new Activity<int>();
            var token = new CancellationTokenSource();
            var task = Add(9, token);
            activity.Setup(task, token, TimeSpan.FromMilliseconds(100));

            var count = 0;
            Exception ex = null;

            // When
            try
            {
                count = await activity.Run();
            }
            catch (Exception e)
            {
                ex = e;
            }

            // Then
            Assert.That(ex, Is.TypeOf<OperationCanceledException>());
            Assert.That(count, Is.EqualTo(0));
        }

        private static async Task<int> Add(int integer, CancellationTokenSource cancellationTokenSource = null)
        {
            await Task.Delay(500);
            if (cancellationTokenSource != null && cancellationTokenSource.IsCancellationRequested)
            {
                cancellationTokenSource.Token.ThrowIfCancellationRequested();
            }
            return integer + 1;
        }
    }
}