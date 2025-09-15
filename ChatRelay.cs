using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Events;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;

namespace ChatRelay;

public class ChatRelayConfig : BasePluginConfig
{
    [JsonPropertyName("IgnoreCommands")] public bool IgnoreCommands { get; set; } = true;

    [JsonPropertyName("OnlyShowCommands")] public bool OnlyShowCommands { get; set; } = false;

    [JsonPropertyName("ApiUrl")] public string ApiUrl { get; set; } = "";

    [JsonPropertyName("ApiAuthHeaderName")] public string ApiAuthHeaderName { get; set; } = "";

    [JsonPropertyName("ApiAuthHeaderValue")] public string ApiAuthHeaderValue { get; set; } = "";

    // Preferred for typical CS2 plugin configs: a single webhook/url to post to
    [JsonPropertyName("WebhookUrl")] public string WebhookUrl { get; set; } = "";

    [JsonPropertyName("DebugLogs")] public bool DebugLogs { get; set; } = true;

    // When true, treat WebhookUrl as a Discord Webhook and send Discord-compatible payloads.
    [JsonPropertyName("DiscordWebhook")] public bool DiscordWebhook { get; set; } = false;
}

[MinimumApiVersion(338)]
public class ChatRelay : BasePlugin, IPluginConfig<ChatRelayConfig>
{
    public override string ModuleName => "ChatRelay";
    public override string ModuleDescription => "Relay chat messages from your CS2 server to API";
    public override string ModuleAuthor => "AGA";
    public override string ModuleVersion => "1.0.0";

    public ChatRelayConfig Config { get; set; } = new();

    public void OnConfigParsed(ChatRelayConfig config)
    {
        Config = config;
    }
    public override void Load(bool hotReload)
    {
        LogInfo($"loaded successfully! (Version {ModuleVersion})");
        // Ensure a config file exists at the requested location on first load
        EnsureConfigFile();
        RegisterEventHandler<EventPlayerChat>(OnEventPlayerChat);
    }

    public override void Unload(bool hotReload)
    {
        // No-op
    }

    public HookResult OnEventPlayerChat(EventPlayerChat @event, GameEventInfo info)
    {
        var eventplayer = @event.Userid;
        var player = Utilities.GetPlayerFromUserid(eventplayer);
        if (player == null || !player.IsValid ||  @event.Text == null)
            return HookResult.Continue;

        // Trim and ignore empty/whitespace-only chat events (happens when player opens chat and presses Enter to close)
        var text = @event.Text.Trim();
        if (string.IsNullOrWhiteSpace(text))
            return HookResult.Continue;

        if (Config.IgnoreCommands)
        {
            if (text.Contains('!') || text.Contains('/'))
                return HookResult.Continue;
        }

        if (Config.OnlyShowCommands)
        {
            if (!text.Contains('!') && !text.Contains('/'))
                return HookResult.Continue;
        }

        string playerteam = "[ALL]";
        if (@event.Teamonly)
        {
            switch ((CsTeam)player.TeamNum)
            {
                case CsTeam.Terrorist:
                    playerteam = "[T]";
                    break;
                case CsTeam.CounterTerrorist:
                    playerteam = "[CT]";
                    break;
                case CsTeam.Spectator:
                    playerteam = "[SPEC]";
                    break;
                case CsTeam.None:
                    playerteam = "[NONE]";
                    break;
            }
        }


        // Normalize SteamID to 64-bit community ID. Some servers provide AccountID (32-bit)
        // which needs base offset added.
        var steam64 = ToSteam64(player.SteamID);
        LogInfo($"ChatRelay: normalized SteamID64 for {player.PlayerName}: {steam64}");
        _ = SendApiMessage(player.PlayerName, text, playerteam, steam64);
        return HookResult.Continue;
    }

    public async Task SendApiMessage(string playerName, string msg, string playerteam, ulong steamID)
    {
        // Defensive: do not send empty/whitespace-only messages
        if (string.IsNullOrWhiteSpace(msg))
        {
            return;
        }

        // Determine endpoint: prefer WebhookUrl, fallback to ApiUrl (backward compatibility)
        var endpoint = string.IsNullOrWhiteSpace(Config.WebhookUrl) ? Config.ApiUrl : Config.WebhookUrl;

        // Guard: require an endpoint to be configured
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            LogInfo("ChatRelay: No WebhookUrl/ApiUrl configured; skipping sending message to API.");
            return;
        }

        using (var httpClient = new HttpClient())
        {
            if (Config.DiscordWebhook)
            {
                // Discord mode: ignore custom auth headers and send Discord-compatible JSON (embed)
                var discordPayload = DiscordSender.BuildChatEmbed(playerName, msg, playerteam, steamID.ToString(), "ChatRelay");
                var discordJson = Newtonsoft.Json.JsonConvert.SerializeObject(discordPayload);
                var discordContent = new StringContent(discordJson, Encoding.UTF8, "application/json");
                var discordResponse = await httpClient.PostAsync(endpoint, discordContent);
                if (!discordResponse.IsSuccessStatusCode)
                {
                    LogInfo($"Discord webhook error: {discordResponse.StatusCode}");
                }
                return;
            }

            // Generic mode: optional auth header
            if (!string.IsNullOrWhiteSpace(Config.ApiAuthHeaderName))
            {
                try
                {
                    httpClient.DefaultRequestHeaders.Remove(Config.ApiAuthHeaderName);
                    httpClient.DefaultRequestHeaders.TryAddWithoutValidation(Config.ApiAuthHeaderName, Config.ApiAuthHeaderValue);
                }
                catch
                {
                    // ignore header issues, request will proceed without auth
                }
            }

            // Generic payload your API can accept; adjust your receiver accordingly
            var payload = new
            {
                playerName = playerName,
                message = msg,
                team = playerteam,
                // Send as string to avoid precision loss in JS/JSON parsers (2^53 issue)
                steamId = steamID.ToString()
            };

            var jsonPayload = Newtonsoft.Json.JsonConvert.SerializeObject(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(endpoint, content);

            if (!response.IsSuccessStatusCode)
            {
                LogInfo($"Error while sending message to API! code: {response.StatusCode}");
            }
        }
    }

    private const ulong Steam64Base = 76561197960265728UL; // offset to convert AccountID to Steam64

    private ulong ToSteam64(ulong id)
    {
        try
        {
            // If it's a bot or not authorized yet, return 0
            if (id == 0)
                return 0;

            // Already Steam64
            if (id >= Steam64Base)
                return id;

            // Likely AccountID (32-bit). Convert to Steam64
            return Steam64Base + id;
        }
        catch
        {
            return id; // fallback without throwing
        }
    }

    private void EnsureConfigFile()
    {
        try
        {
            // Target path must match plugin namespace/folder name exactly
            var configDir = Path.Combine("game", "csgo", "addons", "counterstrikesharp", "configs", "plugins", "ChatRelay");
            var configPath = Path.Combine(configDir, "config.json");

            // Create directory if missing
            if (!Directory.Exists(configDir))
            {
                Directory.CreateDirectory(configDir);
            }

            // If file already exists, do not overwrite
            if (File.Exists(configPath))
            {
                return;
            }

            // Serialize current config with property names preserved
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            var json = JsonSerializer.Serialize(Config, options);
            File.WriteAllText(configPath, json, Encoding.UTF8);
            LogInfo($"ChatRelay: Created default config at {configPath}");
        }
        catch (Exception ex)
        {
            LogError(ex, "ChatRelay: Failed to create default config file");
        }
    }

    private void LogInfo(string message)
    {
        try
        {
            if (Config?.DebugLogs == true)
            {
                Logger.LogInformation(message);
            }
        }
        catch
        {
            // ignore logging failures
        }
    }

    private void LogError(Exception ex, string message)
    {
        try
        {
            if (Config?.DebugLogs == true)
            {
                Logger.LogError(ex, message);
            }
        }
        catch
        {
            // ignore logging failures
        }
    }
}