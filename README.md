# 🧠 FocusTerminal.AI 2.0 (AAA Edition)

An intelligent deep-work terminal assistant that keeps you in the flow. It dynamically monitors your context and clipboard, analyzing your focus in real-time with **Local LLMs (Ollama)**, **OpenRouter**, **Custom OpenAI-compatible APIs**, or **Google Gemini**, backed by a **zero-dependency offline heuristic engine** and **military-grade clipboard privacy filters**.

Built with modern **.NET 9** and **Spectre.Console**.

---

## ✨ Features

- 🤖 **Multi-Provider Hybrid AI Engine:**
  - **Local-First with Ollama:** Run models like `llama3.2:3b`, `phi3.5`, `gemma2:2b`, or `qwen2.5` directly on your CPU/GPU with zero latency and 100% privacy.
  - **OpenRouter:** Access hundreds of open & commercial models (including free-tier models).
  - **Custom API:** Connect to LM Studio, vLLM, LocalAI, or any OpenAI-compatible server.
  - **Google Gemini:** Cloud-based Gemini 1.5 / 2.0 Flash support.
  - **Offline Heuristic Fallback:** Rule-based keyword and code pattern analyzer that works 100% offline with zero external dependencies.
- 🛡️ **Zero-Leak Clipboard Sanitizer:**
  - Automatically scrubs and redacts API keys (`sk-`, `ghp_`, `gho_`, `AKIA`, `AIzaSy`), JWT tokens, private keys, database URLs with passwords, and authorization headers before LLM ingestion.
  - Optional **Strict Privacy Mode** that obfuscates all text content.
- 🎨 **AAA Terminal UI (Spectre.Console):**
  - Live progress bars, non-blocking single-key controls (`[Space]` to pause/resume, `[S]` for quick stats, `[Q]` to quit).
  - Interactive selection prompts and formatted summary tables.
- 📊 **Productivity Analytics & Focus Score:**
  - Persists session history, tracks deep work hours, total checkpoints, distractions, and calculates your **Focus Score (%)**.
- 🌤️ **Configurable Weather Widget:**
  - Live weather report via Open-Meteo with customizable cities and coordinates.
- 🎵 **Adaptive Music Recommendations:**
  - Contextual playlists tailored to your work mode (`work`, `study`, `creative`, or custom).
- 🔔 **Subtle Terminal Audio Notifications:**
  - Audio bell and chime alerts when focus intervals conclude or warnings occur.

---

## 📋 Requirements

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) (Linux, macOS, Windows)
- *(Optional)* [Ollama](https://ollama.com/) for local offline LLMs.
- *(Optional)* API key for OpenRouter or Google Gemini.

---

## ⚡ Quick Start

### 1. Clone & Enter Repository

```bash
git clone https://github.com/Yeri0101/FocusTerminal.AI.git
cd FocusTerminal.AI
```

### 2. Configure Settings

Copy the example configuration:

```bash
cp appsettings.example.json appsettings.json
```

Edit `appsettings.json` according to your preferred setup:

```json
{
  "AiProvider": "Auto",
  "Ollama": {
    "Endpoint": "http://localhost:11434",
    "Model": "llama3.2:3b",
    "TimeoutSeconds": 30
  },
  "OpenRouter": {
    "ApiKey": "YOUR_OPENROUTER_API_KEY",
    "Model": "meta-llama/llama-3.2-3b-instruct:free",
    "Endpoint": "https://openrouter.ai/api/v1"
  },
  "CustomApi": {
    "Endpoint": "http://localhost:1234/v1",
    "ApiKey": "",
    "Model": "local-model"
  },
  "Gemini": {
    "ApiKey": "YOUR_GEMINI_API_KEY",
    "Model": "gemini-1.5-flash"
  },
  "Privacy": {
    "SanitizeClipboard": true,
    "StrictPrivacyMode": false
  },
  "Weather": {
    "City": "Lausanne",
    "Latitude": 46.52,
    "Longitude": 6.63
  },
  "Audio": {
    "EnableSoundAlerts": true
  }
}
```

> **Note:** If `AiProvider` is set to `"Auto"`, FocusTerminal automatically attempts:
> 1. Local **Ollama** (if running)
> 2. **OpenRouter** (if API key configured)
> 3. **Google Gemini** (if API key configured)
> 4. **Custom API** (if endpoint reachable)
> 5. **Heuristic Offline** (guaranteed fallback, never crashes)

### 3. Run the Application

```bash
dotnet run
```

### 4. Run Unit Tests

```bash
dotnet test
```

---

## 🎮 Interactive Controls

During a deep work session, control the timer without typing long words:

| Key | Action |
|:---:|:---|
| <kbd>Space</kbd> | **Pause / Resume** session and view interval details |
| <kbd>S</kbd> | View **Live Stats** (Focus checks & score) |
| <kbd>Q</kbd> | **Stop** interval and generate session summary |

---

## 🏗️ Architecture

```
FocusTerminal.AI/
├── src/
│   ├── Core/
│   │   ├── Models/            # TaskDetails, FocusResult, SessionHistory, AppSettings
│   │   └── Interfaces/        # IAiFocusEngine, IClipboardSanitizer, IFocusMonitor, etc.
│   ├── AI/
│   │   ├── OllamaAiEngine.cs      # Local models via Ollama REST API
│   │   ├── OpenRouterAiEngine.cs  # OpenRouter cloud integration
│   │   ├── CustomOpenAiEngine.cs  # LM Studio / vLLM / LocalAI
│   │   ├── GeminiAiEngine.cs      # Google Gemini integration
│   │   ├── HeuristicAiEngine.cs   # 100% offline rule-based classifier
│   │   └── SmartAiRouter.cs       # Intelligent fallback negotiation
│   ├── Security/
│   │   └── ClipboardSanitizer.cs  # Credential, token & secret scrubbing
│   ├── Services/
│   │   ├── FocusMonitor.cs        # Thread-safe clipboard observer
│   │   ├── WeatherService.cs      # Open-Meteo weather provider
│   │   ├── StorageService.cs      # JSON task & session persistence
│   │   └── AudioNotifier.cs       # Terminal audio alerts
│   └── UI/
│       ├── TerminalDashboard.cs   # Spectre.Console live layout & dialogs
│       └── SessionController.cs   # Interactive session orchestrator
├── tests/
│   └── FocusTerminal.AI.Tests/    # Complete xUnit test suite
├── Program.cs                     # Clean bootstrap orchestrator
├── appsettings.example.json       # Template configuration
└── FocusTerminal.AI.csproj        # .NET 9 project file
```

---

## 📄 License

MIT — Created with ❤️ for developers and deep-work enthusiasts.
