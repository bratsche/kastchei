using System;
using Newtonsoft.Json;

namespace Kastchei
{
    class BroadcastFrame<T>
    {
        [JsonProperty("topic")]   public string  Topic { get; set; }
        [JsonProperty("event")]   public string  Event { get; set; }
        [JsonProperty("payload")] public T       Payload { get; set; }
        [JsonProperty("ref")]     public UInt64? Ref { get; set; }
    }
}
