using System;
using System.Collections.Concurrent;
using System.Timers;
using Timer = System.Timers.Timer;

namespace WindowsFormsApp
{
    public static class MessageBoxQueue
    {
        private static readonly ConcurrentQueue<Action> Queue = new ConcurrentQueue<Action>();
        private static bool _isDisplaingMessage;
        private static Timer _aTimer;

        public static void Add(Action messageBox)
        {
            if (Queue.IsEmpty)
            {
                _aTimer = new Timer();
                _aTimer.Elapsed -= OnTimedEvent;
                _aTimer.Elapsed += OnTimedEvent;
                _aTimer.Interval = 500;
                _aTimer.Enabled = true;
            }

            Queue.Enqueue(messageBox);

            Show();
        }

        private static void OnTimedEvent(object source, ElapsedEventArgs e)
        {
            Show();
        }

        private static void Show()
        {
            if (_isDisplaingMessage)
            {
                return;
            }

            if (Queue.TryDequeue(out var messageBox))
            {
                _isDisplaingMessage = true;
                messageBox();
            }
            
            if (Queue.IsEmpty)
            {
                _aTimer.Enabled = false;
                _aTimer.Elapsed -= OnTimedEvent;
                _aTimer.Dispose();
            }
        }

        public static void SetFree()
        {
            _isDisplaingMessage = false;
        }
    }
}