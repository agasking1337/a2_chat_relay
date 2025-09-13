# ChatRelay Plugin for Counter-Strike Sharp

A plugin that relays chat messages from your CS2 server to an external API.

## Installation

1. Build the project
2. Copy the output files to your CS2 server's plugins directory:
   - `game/csgo/addons/counterstrikesharp/plugins/ChatRelay/`

## Configuration

Create a config file at `game/csgo/addons/counterstrikesharp/configs/plugins/ChatRelay/config.json` with the following structure:

```json
{
  "IgnoreCommands": true,
  "OnlyShowCommands": false,
  "ApiUrl": "https://yourdomain.com/api/chat",
  "ApiAuthHeaderName": "x-api-key",
  "ApiAuthHeaderValue": "YOUR_SUPER_SECRET_KEY",
  "WebhookUrl": "",
  "DebugLogs": true,
  "DiscordWebhook": false
}
```

### Configuration Options

- **IgnoreCommands**: When true, messages starting with ! or / will be ignored
- **OnlyShowCommands**: When true, only messages starting with ! or / will be relayed
- **ApiUrl**: Your API endpoint URL (legacy option, WebhookUrl is preferred)
- **WebhookUrl**: Your webhook URL (preferred over ApiUrl)
- **ApiAuthHeaderName**: Name of the authentication header (e.g., "x-api-key")
- **ApiAuthHeaderValue**: Value of the authentication header (your API key)
- **DebugLogs**: When true, the plugin writes informational/error logs; set to false to silence plugin logs in server console
- **DiscordWebhook**: When true, the plugin treats `WebhookUrl` as a Discord webhook and sends Discord-formatted payloads (ignores ApiUrl/Auth headers)

Note: If the config file does not exist, the plugin will automatically create a default one at startup at `game/csgo/addons/counterstrikesharp/configs/plugins/ChatRelay/config.json`.

## API Payload Format

The plugin sends the following JSON payload to your API:

```json
{
  "playerName": "Alice",
  "message": "hello team",
  "team": "[CT]",
  "steamId": "76561198000000000"
}
```

### Discord Webhook Mode

When `DiscordWebhook` is true, the plugin posts to `WebhookUrl` using Discord's webhook format:

```json
{
  "content": "[CT] Alice: hello team (76561198000000000)",
  "username": "ChatRelay",
  "allowed_mentions": { "parse": [] }
}
```

- No auth headers are sent (Discord webhook URL already encodes authorization).
- Message content is trimmed to 2000 characters to comply with Discord limits.