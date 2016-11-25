using System;
using Newtonsoft.Json;

namespace Kastchei
{
    public class Payload<T>
    {
        [JsonProperty("status")] public string Status { get; set; }
        [JsonProperty("response")] public T Response { get; set; }
    }
}
