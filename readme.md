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
  "WebhookUrl": ""
}
```

### Configuration Options

- **IgnoreCommands**: When true, messages starting with ! or / will be ignored
- **OnlyShowCommands**: When true, only messages starting with ! or / will be relayed
- **ApiUrl**: Your API endpoint URL (legacy option, WebhookUrl is preferred)
- **WebhookUrl**: Your webhook URL (preferred over ApiUrl)
- **ApiAuthHeaderName**: Name of the authentication header (e.g., "x-api-key")
- **ApiAuthHeaderValue**: Value of the authentication header (your API key)

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