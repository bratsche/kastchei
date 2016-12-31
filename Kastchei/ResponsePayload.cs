using System;
using Newtonsoft.Json;

namespace Kastchei
{
    public class ResponsePayload<T>
    {
        [JsonProperty("status")] public string Status { get; set; }
        [JsonProperty("response")] public T Response { get; set; }
    }
}
