# 🧠 FocusTerminal.AI

Una herramienta de terminal que te ayuda a mantenerte enfocado mientras trabajas o estudias. Usa la API de Google Gemini para analizar tu actividad y darte feedback en tiempo real sobre si estás en modo concentración o te has ido por las ramas.

---

## ¿Qué hace exactamente?

Básicamente es un temporizador de trabajo inteligente (estilo Pomodoro, pero más flexible). Mientras trabajas, la app monitorea qué estás copiando al portapapeles y cada cierto tiempo le pregunta a Gemini: *"¿esta persona está enfocada en su tarea o se está distrayendo?"*. Dependiendo de la respuesta, te da un mensaje motivacional o te recuerda que vuelvas al trabajo.

También incluye:

- **Sesiones con intervalos configurables** — tú decides cuántos minutos dura cada bloque de trabajo
- **Recomendaciones de música** — según el modo que elijas (`study`, `work`, `creative`), te sugiere una playlist de Spotify o YouTube
- **Clima en tiempo real** — muestra el tiempo actual en Lausanne al iniciar y al pausar
- **Guardado de tareas** — si cierras la app sin terminar, la próxima vez te pregunta si quieres retomar donde dejaste
- **Pausa inteligente** — al pausar te muestra un resumen de cuánto llevabas y cuánto te falta
- **Frases motivacionales y datos curiosos** — al terminar cada intervalo te da un pequeño empujón

---

## Requisitos

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- Una API Key de Google Gemini — la puedes conseguir gratis en [Google AI Studio](https://aistudio.google.com/app/apikey)

---

## Instalación y configuración

**1. Clona el repositorio**

```bash
git clone https://github.com/Yeri0101/FocusTerminal.AI.git
cd FocusTerminal.AI
```

**2. Configura tu API Key**

Copia el archivo de ejemplo y ponle tu clave:

```bash
cp appsettings.example.json appsettings.json
```

Abre `appsettings.json` y reemplaza el placeholder con tu key real:

```json
{
  "ApiKey": "TU_API_KEY_DE_GEMINI_AQUÍ"
}
```

> ⚠️ **Nunca subas tu `appsettings.json` al repositorio.** Ya está en el `.gitignore` para que no lo hagas por accidente.

**3. Restaura dependencias y ejecuta**

```bash
dotnet restore
dotnet run
```

---

## Cómo usarla

Al iniciar, la app te pide:
- Nombre de la tarea
- Una descripción breve
- Duración del intervalo en minutos
- Modo de trabajo: `study`, `work`, `creative` (o uno personalizado)

Durante la sesión puedes escribir:

| Comando  | Acción                               |
|----------|--------------------------------------|
| `stop`   | Termina la sesión                    |
| `pause`  | Pausa y muestra resumen del progreso |
| `resume` | Reanuda tras una pausa               |

Los chequeos de enfoque ocurren automáticamente cada 15% del intervalo, siempre que hayas copiado algo al portapapeles.

---

## Estructura del proyecto

```
FocusTerminal.AI/
├── Program.cs               # Toda la lógica de la aplicación
├── appsettings.example.json # Plantilla de configuración (segura para commitear)
├── appsettings.json         # Tu configuración real con la API Key (NO commitear)
├── FocusTerminal.AI.csproj  # Proyecto .NET 9
└── FocusTerminal.AI.sln     # Solución de Visual Studio
```

---

## Dependencias

| Paquete | Uso |
|---------|-----|
| `Microsoft.Extensions.Configuration.Json` | Leer la API Key desde `appsettings.json` |
| `TextCopy` | Acceder al portapapeles de forma multiplataforma |

---

## Notas

- El clima está configurado para Lausanne (coordenadas `46.52, 6.63`). Si estás en otra ciudad, cambia las coordenadas en el método `WeatherService.GetWeatherAsync()` dentro de `Program.cs`.
- Si no configuras la API Key, la app sigue funcionando pero asume que siempre estás enfocado (sin análisis real de Gemini).
- El archivo `task_config.txt` se genera automáticamente al guardar una tarea; está en el `.gitignore`.

---

## Licencia

MIT — úsalo, modifícalo, compártelo.
