using System;
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
            activity.Setup(task, 600);

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
            var task = Add(9);

            // When
            var count = await activity.ForTask(task).Wait(600).Run();

            // Then
            Assert.That(count, Is.EqualTo(10));
        }

        [Test]
        public async Task Throw_error_when_timeout()
        {
            // Given
            var activity = new Activity<int>();
            var task = Add(9);
            activity.Setup(task, 100);

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
                Console.WriteLine(e);
            }

            // Then
            Assert.That(ex, Is.TypeOf<OperationCanceledException>());
            Assert.That(count, Is.EqualTo(0));
        }

        private static async Task<int> Add(int integer)
        {
            await Task.Delay(500);
            return integer + 1;
        }
    }
}