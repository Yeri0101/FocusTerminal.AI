using System;
using FocusTerminal.AI.Core.Models;
using FocusTerminal.AI.Security;
using Xunit;

namespace FocusTerminal.AI.Tests
{
    public class ClipboardSanitizerTests
    {
        [Fact]
        public void Sanitize_ShouldRedactApiKey_WhenGithubTokenIsPresent()
        {
            var sanitizer = new ClipboardSanitizer();
            string input = "Mi token es ghp_1234567890abcdefghijklmnopqrstuvwxyz para repo";

            string result = sanitizer.Sanitize(input);

            Assert.DoesNotContain("ghp_1234567890abcdefghijklmnopqrstuvwxyz", result);
            Assert.Contains("[REDACTED_API_KEY]", result);
        }

        [Fact]
        public void Sanitize_ShouldRedactOpenAiApiKey()
        {
            var sanitizer = new ClipboardSanitizer();
            string input = "sk-proj-123456789012345678901234567890";

            string result = sanitizer.Sanitize(input);

            Assert.DoesNotContain("sk-proj-", result);
            Assert.Contains("[REDACTED_API_KEY]", result);
        }

        [Fact]
        public void Sanitize_ShouldRedactJwtToken()
        {
            var sanitizer = new ClipboardSanitizer();
            string input = "Authorization: eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyfQ.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c";

            string result = sanitizer.Sanitize(input);

            Assert.DoesNotContain("eyJhbGci", result);
            Assert.Contains("[REDACTED_JWT_TOKEN]", result);
        }

        [Fact]
        public void Sanitize_ShouldRedactCredentialsInUrl()
        {
            var sanitizer = new ClipboardSanitizer();
            string input = "postgres://admin:superSecret123@database.internal:5432/app";

            string result = sanitizer.Sanitize(input);

            Assert.DoesNotContain("superSecret123", result);
            Assert.Contains("[REDACTED_PASSWORD]", result);
        }

        [Fact]
        public void Sanitize_StrictPrivacyMode_ShouldMaskAllContent()
        {
            var config = new PrivacyConfig { StrictPrivacyMode = true };
            var sanitizer = new ClipboardSanitizer(config);
            string input = "git commit -m 'feat: something sensitive'";

            string result = sanitizer.Sanitize(input);

            Assert.StartsWith("[Texto protegido:", result);
            Assert.DoesNotContain("something sensitive", result);
        }

        [Fact]
        public void ContainsSensitiveData_ShouldReturnTrue_ForBearerToken()
        {
            var sanitizer = new ClipboardSanitizer();
            string input = "Bearer a1b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6";

            bool isSensitive = sanitizer.ContainsSensitiveData(input);

            Assert.True(isSensitive);
        }
    }
}
