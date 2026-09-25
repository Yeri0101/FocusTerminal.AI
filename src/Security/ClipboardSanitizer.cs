using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using FocusTerminal.AI.Core.Interfaces;
using FocusTerminal.AI.Core.Models;

namespace FocusTerminal.AI.Security
{
    public class ClipboardSanitizer : IClipboardSanitizer
    {
        private readonly PrivacyConfig _config;

        private static readonly Regex ApiKeyPattern = new(
            @"\b(sk-[a-zA-Z0-9\-_]{20,}|ghp_[a-zA-Z0-9]{30,}|gho_[a-zA-Z0-9]{30,}|glpat-[a-zA-Z0-9\-]{20,}|xox[baprs]-[a-zA-Z0-9\-]{10,}|AKIA[0-9A-Z]{16}|AIzaSy[a-zA-Z0-9_-]{33})\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex JwtPattern = new(
            @"\beyJ[a-zA-Z0-9\-_]{10,}\.eyJ[a-zA-Z0-9\-_]{10,}\.[a-zA-Z0-9\-_]{10,}\b",
            RegexOptions.Compiled);

        private static readonly Regex BearerOrAuthPattern = new(
            @"\b(Bearer\s+[a-zA-Z0-9\-_\.=]+|Basic\s+[a-zA-Z0-9+/=]+)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex PrivateKeyPattern = new(
            @"-----BEGIN [A-Z ]+PRIVATE KEY-----[\s\S]*?-----END [A-Z ]+PRIVATE KEY-----",
            RegexOptions.Compiled);

        private static readonly Regex PasswordUrlPattern = new(
            @"(://[^:]+:)([^@]+)(@)",
            RegexOptions.Compiled);

        private static readonly Regex GenericSecretAssignmentPattern = new(
            @"(password|passwd|pwd|secret|token|apikey|api_key)\s*[:=]\s*['""][^'""]+['""]",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public ClipboardSanitizer(PrivacyConfig? config = null)
        {
            _config = config ?? new PrivacyConfig();
        }

        public string Sanitize(string rawText, int maxLength = 150)
        {
            if (string.IsNullOrWhiteSpace(rawText))
                return string.Empty;

            if (_config.StrictPrivacyMode)
            {
                return $"[Texto protegido: {rawText.Length} caracteres, tipo: {CategorizeText(rawText)}]";
            }

            if (!_config.SanitizeClipboard)
            {
                return Truncate(rawText, maxLength);
            }

            string sanitized = rawText;

            // Censurar claves privadas completas
            sanitized = PrivateKeyPattern.Replace(sanitized, "[REDACTED_PRIVATE_KEY]");

            // Censurar JWTs
            sanitized = JwtPattern.Replace(sanitized, "[REDACTED_JWT_TOKEN]");

            // Censurar API Keys conocidas
            sanitized = ApiKeyPattern.Replace(sanitized, "[REDACTED_API_KEY]");

            // Censurar Bearer / Auth tokens
            sanitized = BearerOrAuthPattern.Replace(sanitized, "Bearer [REDACTED_AUTH_TOKEN]");

            // Censurar credenciales en URLs (ej: postgres://user:pass@host)
            sanitized = PasswordUrlPattern.Replace(sanitized, "$1[REDACTED_PASSWORD]$3");

            // Censurar asignaciones directas de contraseñas/secretos
            sanitized = GenericSecretAssignmentPattern.Replace(sanitized, "$1=[REDACTED]");

            return Truncate(sanitized, maxLength);
        }

        public IReadOnlyList<string> SanitizeBatch(IEnumerable<string> snippets, int maxLength = 150)
        {
            if (snippets == null) return Array.Empty<string>();
            return snippets
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => Sanitize(s, maxLength))
                .ToList();
        }

        public bool ContainsSensitiveData(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;

            return ApiKeyPattern.IsMatch(text)
                || JwtPattern.IsMatch(text)
                || BearerOrAuthPattern.IsMatch(text)
                || PrivateKeyPattern.IsMatch(text)
                || PasswordUrlPattern.IsMatch(text)
                || GenericSecretAssignmentPattern.IsMatch(text);
        }

        private static string Truncate(string text, int maxLength)
        {
            if (text.Length <= maxLength) return text;
            return text.Substring(0, maxLength) + "…";
        }

        private static string CategorizeText(string text)
        {
            if (text.Contains("{") && text.Contains("}")) return "código/json";
            if (text.StartsWith("http://") || text.StartsWith("https://")) return "url";
            if (text.Contains("git ") || text.Contains("dotnet ") || text.Contains("npm ")) return "comando";
            return "texto general";
        }
    }
}
