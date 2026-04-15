# 🧠 FocusTerminal.AI

A terminal tool that helps you stay focused while working or studying. It uses the Google Gemini API to analyze your activity and give you real-time feedback on whether you're in deep focus mode or drifting off.

---

## What does it do?

Think of it as a smart work timer (Pomodoro-style, but more flexible). While you work, the app monitors what you copy to your clipboard and periodically asks Gemini: *"Is this person focused on their task or getting distracted?"*. Based on the response, it either sends you a motivational message or gently nudges you back on track.

It also includes:

- **Configurable work intervals** — you decide how many minutes each work block lasts
- **Music recommendations** — based on your chosen mode (`study`, `work`, `creative`), it suggests a Spotify or YouTube playlist
- **Live weather** — shows the current weather in Lausanne when you start a session or pause
- **Task saving** — if you close the app before finishing, next time it asks if you want to pick up where you left off
- **Smart pause** — pausing shows a summary of how much time you've done and how much is left
- **Motivational quotes and tech facts** — a small boost at the end of each interval

---

## Requirements

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- A Google Gemini API Key — get one for free at [Google AI Studio](https://aistudio.google.com/app/apikey)

---

## Setup

**1. Clone the repository**

```bash
git clone https://github.com/Yeri0101/FocusTerminal.AI.git
cd FocusTerminal.AI
```

**2. Configure your API Key**

Copy the example config file and add your key:

```bash
cp appsettings.example.json appsettings.json
```

Open `appsettings.json` and replace the placeholder with your actual key:

```json
{
  "ApiKey": "YOUR_GEMINI_API_KEY_HERE"
}
```

**3. Restore dependencies and run**

```bash
dotnet restore
dotnet run
```

---

## How to use it

When you launch the app, it asks for:
- Task name
- A short description
- Interval duration in minutes
- Work mode: `study`, `work`, `creative` (or a custom one)

During a session you can type:

| Command  | Action                                  |
|----------|-----------------------------------------|
| `stop`   | End the session                         |
| `pause`  | Pause and show a progress summary       |
| `resume` | Resume after a pause                    |

Focus checks happen automatically every 15% of the interval, as long as you've copied something to the clipboard.

---

## Project structure

```
FocusTerminal.AI/
├── Program.cs               # All application logic
├── appsettings.example.json # Config template (safe to commit)
├── appsettings.json         # Your local config with the API Key (git-ignored)
├── FocusTerminal.AI.csproj  # .NET 9 project file
└── FocusTerminal.AI.sln     # Visual Studio solution
```

---

## Dependencies

| Package | Purpose |
|---------|---------|
| `Microsoft.Extensions.Configuration.Json` | Read the API Key from `appsettings.json` |
| `TextCopy` | Cross-platform clipboard access |

---

## Notes

- Weather is hardcoded to Lausanne (coordinates `46.52, 6.63`). To change your city, update the coordinates in `WeatherService.GetWeatherAsync()` inside `Program.cs`.
- If no API Key is configured, the app still runs but assumes you're always focused (no real Gemini analysis).
- `task_config.txt` is generated automatically when a task is saved and is git-ignored.

---

## License

MIT — use it, modify it, share it.
