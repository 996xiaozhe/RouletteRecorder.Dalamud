using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using RouletteRecorder.Dalamud.Network.DungeonLogger;
using RouletteRecorder.Dalamud.Utils;
using System;
using System.Numerics;
using System.Threading.Tasks;

namespace RouletteRecorder.Dalamud.Windows;

public sealed class ConfigWindow : Window, IDisposable
{
    private enum LoginStatus
    {
        Initial,
        Pending,
        Success,
        Failed
    }

    private string loginResponseMessage = string.Empty;
    private LoginStatus loginStatus = LoginStatus.Initial;

    public ConfigWindow(Plugin plugin) : base("Config###rouletteRecorderConfigWindow", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(375, 425),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    public void Dispose() { }

    public override void Draw()
    {
        if (ImGui.CollapsingHeader(Plugin.Localization.Localize("Subscribed Roulette Types")))
        {
            ImGui.Indent();
            foreach (var roulette in Database.CfRoulettes)
            {
                var selected = Plugin.Configuration.SubscribedRouletteIds.Contains(roulette.RowId);
                if (ImGui.Checkbox(roulette.Name.ToString(), ref selected))
                {
                    Plugin.Configuration.SetSubscribedRouletteId(roulette, selected);
                }
            }
            ImGui.Unindent();
        }

        if (ImGui.CollapsingHeader(Plugin.Localization.Localize("DungeonLogger Account Config")))
        {
            ImGui.Indent();
            if (ImGui.Checkbox(Plugin.Localization.Localize("Enable DungeonLogger Report"), ref Plugin.Configuration.DungeonLoggerConfig.Enabled))
            {
                Plugin.Configuration.Save();
            }

            var apiNames = new[]
            {
                Plugin.Localization.Localize("New API"),
                Plugin.Localization.Localize("Legacy API"),
            };
            var apiIndex = Plugin.Configuration.DungeonLoggerConfig.ApiMode == DungeonLoggerApiMode.Legacy ? 1 : 0;

            ImGui.Text(Plugin.Localization.Localize("Server URL"));
            ImGui.SameLine();
            ImGui.SetNextItemWidth(210);
            if (ImGui.InputText("##dungeonLoggerServerUrl", ref Plugin.Configuration.DungeonLoggerConfig.ServerUrl, 512))
            {
                Plugin.Configuration.Save();
            }

            ImGui.Text(Plugin.Localization.Localize("API Mode"));
            ImGui.SameLine();
            if (ImGui.Combo("##dungeonLoggerApiMode", ref apiIndex, apiNames, apiNames.Length))
            {
                Plugin.Configuration.DungeonLoggerConfig.ApiMode = apiIndex == 0 ? DungeonLoggerApiMode.New : DungeonLoggerApiMode.Legacy;
                Plugin.Configuration.Save();
            }

            if (ImGui.SmallButton(Plugin.Localization.Localize("Use new API default URL")))
            {
                Plugin.Configuration.DungeonLoggerConfig.ApiMode = DungeonLoggerApiMode.New;
                Plugin.Configuration.DungeonLoggerConfig.ServerUrl = DungeonLoggerConfig.DefaultNewApiServerUrl;
                Plugin.Configuration.Save();
            }

            ImGui.SameLine();
            if (ImGui.SmallButton(Plugin.Localization.Localize("Use legacy API default URL")))
            {
                Plugin.Configuration.DungeonLoggerConfig.ApiMode = DungeonLoggerApiMode.Legacy;
                Plugin.Configuration.DungeonLoggerConfig.ServerUrl = DungeonLoggerConfig.DefaultLegacyApiServerUrl;
                Plugin.Configuration.Save();
            }

            ImGui.TextWrapped(Plugin.Localization.Localize("Server address should be the website root and must not include /api. New API mode sends full records with roulette type, duration, time, source id and party; legacy API mode sends only mazeId and profKey."));

            if (Plugin.Configuration.DungeonLoggerConfig.Enabled)
            {
                ImGui.Text(Plugin.Localization.Localize("User Name"));
                ImGui.SameLine();
                if (ImGui.InputText("##username", ref Plugin.Configuration.DungeonLoggerConfig.Username, 100))
                {
                    Plugin.Configuration.Save();
                }

                ImGui.Text(Plugin.Localization.Localize("Password"));
                ImGui.SameLine();
                if (ImGui.InputText("##password", ref Plugin.Configuration.DungeonLoggerConfig.Password, 100, ImGuiInputTextFlags.Password))
                {
                    Plugin.Configuration.Save();
                }
            }

            if (ImGui.Button(Plugin.Localization.Localize("Test Login")))
            {
                Task.Run(TestDungeonLoggerLogin);
            }

            var loginStatusColor = loginStatus switch
            {
                LoginStatus.Pending => ImGuiColors.DalamudYellow,
                LoginStatus.Success => ImGuiColors.ParsedGreen,
                LoginStatus.Failed => ImGuiColors.DalamudRed,
                _ => ImGuiColors.DalamudWhite
            };

            const string pendingMessage = "Sending request to Dungeon Logger Server";
            ImGui.TextColored(loginStatusColor, Plugin.Localization.Localize(loginStatus == LoginStatus.Pending ? pendingMessage : loginResponseMessage));

            ImGui.Unindent();
        }
    }

    public async Task TestDungeonLoggerLogin()
    {
        loginStatus = LoginStatus.Pending;
        var username = Plugin.Configuration.DungeonLoggerConfig.Username;
        var password = Plugin.Configuration.DungeonLoggerConfig.Password;
        if (username.IsNullOrEmpty() || password.IsNullOrEmpty())
        {
            loginStatus = LoginStatus.Failed;
            loginResponseMessage = "Username or Password is empty";
            return;
        }

        try
        {
            var config = Plugin.Configuration.DungeonLoggerConfig;
            using var client = new DungeonLoggerClient(config.ServerUrl, config.ApiMode);
            var response = await client.PostLogin(password, username);
            loginStatus = response?.Code != 0 ? LoginStatus.Failed : LoginStatus.Success;
            loginResponseMessage = response?.Msg ?? string.Empty;
        }
        catch (Exception e)
        {
            Plugin.PluginLog.Error(e, "Request failed when logging into DungeonLogger Server");

            loginStatus = LoginStatus.Failed;
            loginResponseMessage = e.ToString();
        }
    }
}
