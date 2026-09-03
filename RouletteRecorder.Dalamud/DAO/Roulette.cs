using Dalamud.Utility;
using RouletteRecorder.Dalamud.Network.DungeonLogger;
using RouletteRecorder.Dalamud.Network.DungeonLogger.Structures;
using RouletteRecorder.Dalamud.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace RouletteRecorder.Dalamud.DAO;

public class Roulette(string? contentName, string? rouletteType, bool isCompleted = false, uint? contentRouletteId = null)
{
    public string? RouletteType { get; set; } = rouletteType;
    public string Date { get; set; } = DateTime.Now.ToString("yyyy-MM-dd");
    public string StartedAt { get; set; } = DateTime.Now.ToString("T");
    public string? EndedAt { get; set; }
    public string? ContentName { get; set; } = contentName;
    public string? JobName { get; set; }
    public bool IsCompleted { get; set; } = isCompleted;
    public uint? ContentRouletteId { get; set; } = contentRouletteId;
    public DateTimeOffset StartedAtOffset { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset? EndedAtOffset { get; set; }
    public static Roulette? Instance { get; private set; }

    public static void Init(string? contentName = null, string? rouletteType = null, bool isCompleted = false, uint? contentRouletteId = null)
    {
        Instance = new Roulette(contentName, rouletteType, isCompleted, contentRouletteId);
    }

    public static void Init(Roulette instance)
    {
        Instance = instance;
    }

    public async void Finish()
    {
        try
        {
            if (Instance == null) return;

            var currContentRoulette = Database.CfRoulettes.FirstOrDefault(x => x.Name.ToString().Equals(RouletteType));
            var isSubscribedRouletteType = Plugin.Configuration.SubscribedRouletteIds.Contains(currContentRoulette.RowId);
            if (Instance.RouletteType == null || Instance.ContentName == null || !isSubscribedRouletteType) return;

            Instance.JobName = Plugin.GetJobName() ?? "未知职业";
            Instance.EndedAt = DateTime.Now.ToString("T");
            Instance.EndedAtOffset = DateTimeOffset.Now;

            Database.InsertRoulette(Instance);
            if (Instance.IsCompleted && Plugin.Configuration.DungeonLoggerConfig.Enabled) await UploadDungeonLogger();

            Instance = null;
        }
        catch (Exception e)
        {
            Plugin.PluginLog.Error(e, "Failed to finish roulette");
        }
    }

    public static async Task UploadDungeonLogger()
    {
        try
        {
            if (Instance == null)
            {
                Plugin.PluginLog.Debug("null Roulette Instance when uploading to DungeonLogger");
                return;
            }

            var username = Plugin.Configuration.DungeonLoggerConfig.Username;
            var password = Plugin.Configuration.DungeonLoggerConfig.Password;
            if (username.IsNullOrEmpty() || password.IsNullOrEmpty())
            {
                Plugin.PluginLog.Warning("DungeonLogger enabled but username or password is empty");
                return;
            }

            using var client = new DungeonLoggerClient(
                Plugin.Configuration.DungeonLoggerConfig.ServerUrl,
                Plugin.Configuration.DungeonLoggerConfig.ApiMode);
            var login = await client.PostLogin(password, username);
            if (login?.Code != 0) throw new Exception($"login failed: {login?.Msg}");

            var maze = await client.GetStatMaze();
            if (maze?.Data == null) throw new Exception("maze data from DungeonLogger is null");

            var job = await client.GetStatProf();
            if (job?.Data == null) throw new Exception("job data from DungeonLogger is null");

            var mazeId = maze.Data.Find(ele => ele.Name.Equals(Instance.ContentName))?.Id ??
                         throw new Exception("cannot convert to DungeonLogger mazeId");
            var profKey = job.Data.Find(ele => ele.NameCn.Equals(Instance.JobName))?.Key ??
                          throw new Exception("cannot convert to DungeonLogger profKey");

            var payload = new DungeonLoggerRecord
            {
                MazeId = mazeId,
                ProfKey = profKey,
                RouletteId = Instance.ContentRouletteId.HasValue ? (int)Instance.ContentRouletteId.Value : null,
                RouletteType = Instance.RouletteType,
                DurationSeconds = BuildDurationSeconds(Instance),
                OccurredAt = Instance.EndedAtOffset ?? Instance.StartedAtOffset,
                SourceId = BuildSourceId(Instance),
                Party = CapturePartyMembers(),
            };

            var upload = await client.PostRecord(payload);
            if (upload?.Code != 0) throw new Exception(upload?.Msg ?? "unknown upload error");
        }
        catch (Exception e)
        {
            Plugin.PluginLog.Error(e, "Failed to upload roulette result to DungeonLogger");
        }
    }

    private static int? BuildDurationSeconds(Roulette roulette)
    {
        if (!roulette.EndedAtOffset.HasValue || roulette.EndedAtOffset <= roulette.StartedAtOffset) return null;

        return (int?)Math.Clamp(
            (long)Math.Round((roulette.EndedAtOffset.Value - roulette.StartedAtOffset).TotalSeconds),
            1,
            86400);
    }

    private static string BuildSourceId(Roulette roulette)
    {
        var seed = $"{roulette.ContentName}|{roulette.RouletteType}|{roulette.StartedAtOffset:O}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
        return "rr-" + Convert.ToHexString(hash).ToLowerInvariant()[..24];
    }

    private static List<DungeonLoggerPartyMember> CapturePartyMembers()
    {
        var result = new List<DungeonLoggerPartyMember>();
        try
        {
            var localPlayerName = Plugin.PlayerState.CharacterName;
            foreach (var member in Plugin.PartyList)
            {
                if (member == null) continue;

                var name = member.Name.TextValue;
                if (string.IsNullOrWhiteSpace(name)) continue;
                if (localPlayerName != null && string.Equals(name, localPlayerName, StringComparison.OrdinalIgnoreCase)) continue;

                var classJob = member.ClassJob.Value;
                result.Add(new DungeonLoggerPartyMember
                {
                    Name = name,
                    World = member.World.IsValid ? member.World.Value.Name.ToString() : null,
                    Job = classJob.Abbreviation.ToString(),
                    Role = RoleName(classJob.Role),
                });
            }
        }
        catch (Exception e)
        {
            Plugin.PluginLog.Warning(e, "Failed to capture party members for DungeonLogger upload");
        }

        return result;
    }

    private static string RoleName(uint role) => role switch
    {
        1 => "TANK",
        4 => "HEALER",
        _ => "DPS",
    };
}
