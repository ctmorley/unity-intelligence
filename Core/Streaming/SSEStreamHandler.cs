using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace UnityAIAssistant.Core.Streaming
{
    /// <summary>
    /// Custom DownloadHandler for Server-Sent Events (SSE) streaming.
    /// Compatible with UnityWebRequest for Unity Editor use.
    /// </summary>
    public class SSEStreamHandler : DownloadHandlerScript
    {
        private readonly StringBuilder buffer = new StringBuilder();
        private readonly Action<SSEEvent> onEventReceived;
        private readonly Action<string> onRawChunk;
        private readonly object lockObject = new object();
        private readonly Queue<SSEEvent> pendingEvents = new Queue<SSEEvent>();

        public SSEStreamHandler(Action<SSEEvent> onEventReceived, Action<string> onRawChunk = null)
        {
            this.onEventReceived = onEventReceived;
            this.onRawChunk = onRawChunk;
        }

        protected override bool ReceiveData(byte[] data, int dataLength)
        {
            lock (lockObject)
            {
                string chunk = Encoding.UTF8.GetString(data, 0, dataLength);
                onRawChunk?.Invoke(chunk);
                buffer.Append(chunk);

                ProcessBuffer();
            }
            return true;
        }

        private void ProcessBuffer()
        {
            string content = buffer.ToString();
            int eventEnd;

            // SSE events are separated by double newlines
            while ((eventEnd = content.IndexOf("\n\n", StringComparison.Ordinal)) >= 0)
            {
                string eventBlock = content.Substring(0, eventEnd);
                content = content.Substring(eventEnd + 2);

                var sseEvent = ParseSSEEvent(eventBlock);
                if (sseEvent != null)
                {
                    pendingEvents.Enqueue(sseEvent);
                }
            }

            buffer.Clear();
            buffer.Append(content);
        }

        private SSEEvent ParseSSEEvent(string eventBlock)
        {
            string eventType = null;
            string data = null;
            string id = null;

            foreach (string line in eventBlock.Split('\n'))
            {
                string trimmedLine = line.Trim();
                if (string.IsNullOrEmpty(trimmedLine)) continue;

                int colonIndex = trimmedLine.IndexOf(':');
                if (colonIndex < 0) continue;

                string field = trimmedLine.Substring(0, colonIndex).Trim();
                string value = colonIndex < trimmedLine.Length - 1
                    ? trimmedLine.Substring(colonIndex + 1).TrimStart()
                    : "";

                switch (field.ToLowerInvariant())
                {
                    case "event":
                        eventType = value;
                        break;
                    case "data":
                        data = data == null ? value : data + "\n" + value;
                        break;
                    case "id":
                        id = value;
                        break;
                }
            }

            if (data == null) return null;
            if (data == "[DONE]") return new SSEEvent { Type = "done", Data = data };

            return new SSEEvent
            {
                Type = eventType ?? "message",
                Data = data,
                Id = id
            };
        }

        /// <summary>
        /// Process pending events on the main thread.
        /// Call this from EditorApplication.update or Update().
        /// </summary>
        public void ProcessPendingEvents()
        {
            lock (lockObject)
            {
                while (pendingEvents.Count > 0)
                {
                    var evt = pendingEvents.Dequeue();
                    try
                    {
                        onEventReceived?.Invoke(evt);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[SSEStreamHandler] Error processing event: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Check if there are pending events to process.
        /// </summary>
        public bool HasPendingEvents
        {
            get
            {
                lock (lockObject)
                {
                    return pendingEvents.Count > 0;
                }
            }
        }

        protected override void CompleteContent()
        {
            // Process any remaining buffer content
            lock (lockObject)
            {
                if (buffer.Length > 0)
                {
                    var finalEvent = ParseSSEEvent(buffer.ToString());
                    if (finalEvent != null)
                    {
                        pendingEvents.Enqueue(finalEvent);
                    }
                    buffer.Clear();
                }
            }
        }
    }

    /// <summary>
    /// Represents a Server-Sent Event.
    /// </summary>
    public class SSEEvent
    {
        public string Type;
        public string Data;
        public string Id;
    }
}
