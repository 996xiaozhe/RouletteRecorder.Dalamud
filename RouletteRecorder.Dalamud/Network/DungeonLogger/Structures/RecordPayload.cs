using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace RouletteRecorder.Dalamud.Network.DungeonLogger.Structures
{
    public sealed class DungeonLoggerRecord
    {
        [JsonProperty("mazeId")]
        public int MazeId { get; set; }

        [JsonProperty("profKey")]
        public string ProfKey { get; set; } = string.Empty;

        [JsonProperty("rouletteId", NullValueHandling = NullValueHandling.Ignore)]
        public int? RouletteId { get; set; }

        [JsonProperty("rouletteType", NullValueHandling = NullValueHandling.Ignore)]
        public string? RouletteType { get; set; }

        [JsonProperty("durationSeconds", NullValueHandling = NullValueHandling.Ignore)]
        public int? DurationSeconds { get; set; }

        [JsonProperty("occurredAt", NullValueHandling = NullValueHandling.Ignore)]
        public DateTimeOffset? OccurredAt { get; set; }

        [JsonProperty("sourceId", NullValueHandling = NullValueHandling.Ignore)]
        public string? SourceId { get; set; }

        [JsonProperty("party", NullValueHandling = NullValueHandling.Ignore)]
        public List<DungeonLoggerPartyMember>? Party { get; set; }
    }

    public sealed class DungeonLoggerPartyMember
    {
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("world", NullValueHandling = NullValueHandling.Ignore)]
        public string? World { get; set; }

        [JsonProperty("job", NullValueHandling = NullValueHandling.Ignore)]
        public string? Job { get; set; }

        [JsonProperty("role", NullValueHandling = NullValueHandling.Ignore)]
        public string? Role { get; set; }
    }
}
