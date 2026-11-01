namespace Jolly.Net.Sse
{
    public readonly struct SseEvent
    {
        public const string DoneSentinel = "[DONE]";

        public SseEvent(string eventName, string data) { EventName = eventName; Data = data; }

        /// <summary>The "event:" field; null when the server sent none.</summary>
        public string EventName { get; }
        /// <summary>All "data:" lines of the event joined with '\n'.</summary>
        public string Data { get; }
        public bool IsDone => Data == DoneSentinel;
    }
}
