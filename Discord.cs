using System;

namespace ChatRelay
{
    public static class DiscordSender
    {
        // Builds a minimal, safe Discord webhook payload using plain content.
        // - content: message text (max 2000 chars in Discord; we trim if needed)
        // - username: shown as the message author in the channel (optional)
        public static object BuildSimpleMessage(string content, string? username = null)
        {
            if (content == null)
                content = string.Empty;

            // Discord content limit is 2000 characters; trim if longer
            if (content.Length > 2000)
            {
                content = content.Substring(0, 1997) + "...";
            }

            // Disallow mentions by default to avoid @everyone/@here
            var payload = new
            {
                content = content,
                username = username,
                allowed_mentions = new
                {
                    parse = Array.Empty<string>()
                }
            };

            return payload;
        }

        // Builds an embed-based message suitable for Discord with light formatting
        // - team: like "[CT]", "[T]", "[SPEC]", "[ALL]"; used to pick color
        // - steamId: ulong or string; turned into a profile link
        public static object BuildChatEmbed(string playerName, string message, string team, string steamId, string? username = "ChatRelay")
        {
            playerName ??= "Unknown";
            message ??= string.Empty;
            team ??= "[ALL]";
            steamId ??= "0";

            // Discord limits
            // - Embed description max 4096 chars
            // - Field name max 256, value max 1024
            var text = message.Length > 4000 ? message.Substring(0, 3997) + "..." : message;
            text = EscapeMarkdown(text);

            // Map team to a color (decimal int)
            // CT: blue, T: orange, SPEC: gray, ALL/NONE: green
            int color = team switch
            {
                "[CT]" => 3447003,   // blue
                "[T]"  => 15105570,  // orange
                "[SPEC]" => 9807270, // grey
                _ => 3066993          // green
            };

            // Steam profile link
            var steamUrl = $"https://steamcommunity.com/profiles/{steamId}";

            var embed = new
            {
                author = new { name = $"{team} {playerName}" },
                description = text,
                color = color,
                timestamp = DateTime.UtcNow.ToString("o"),
                fields = new object[]
                {
                    new { name = "SteamID64", value = SanitizeField($"[{steamId}]({steamUrl})", 1024), inline = true }
                }
            };

            var payload = new
            {
                username = username,
                allowed_mentions = new { parse = Array.Empty<string>() },
                embeds = new[] { embed }
            };

            return payload;
        }

        private static string SanitizeField(string input, int max)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            if (input.Length > max) return input.Substring(0, Math.Max(0, max - 3)) + "...";
            return input;
        }

        private static string EscapeMarkdown(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            // Escape characters that Discord uses for formatting to avoid unintended styling
            // * _ ~ ` > | (backslash escapes)
            var chars = new[] { "\\", "*", "_", "~", "`", ">", "|" };
            foreach (var ch in chars)
            {
                input = input.Replace(ch, "\\" + ch);
            }
            return input;
        }
    }
}
