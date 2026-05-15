// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Net.Http;
using System.Text;
using System.Text.Json;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using static EvolveOS_Optimizer.Core.Enums;

namespace EvolveOS_Optimizer.Utilities.Services
{
    public static class AiExplainerService
    {
        private static readonly HttpClient _http = new();
        private static readonly Dictionary<string, string> _cache = new(StringComparer.OrdinalIgnoreCase);

        private static string TestPrompt => ResourceString.GetString("ai_explainer_test_prompt")
            ?? "Based on the following facts, describe EvolveOS Optimizer in 2 short sentences: " +
               "It is a modern, self-contained Windows management suite built with WinUI 3. " +
               "It combines aggressive system optimization, deep privacy controls, and military-grade security tools into a single lightweight executable. " +
               "Do NOT mention AI, machine learning or any AI-related features. Keep it factual.";

        public static async Task<string> ExplainAsync(CleanerEntry entry)
        {
            if (!await NetworkHelper.IsConnectedAsync())
            {
                return ResourceString.GetString("no_internet_connection_notif_key")
                       ?? "No internet connection detected. Please check your network and try again.";
            }

            if (_cache.TryGetValue(entry.Name, out var cached))
                return cached;

            AiProvider selectedProvider = LocalMachineSettingsEngine.ActiveAiProvider;
            string result;

            string prompt = BuildPrompt(entry);

            if (selectedProvider == AiProvider.Groq)
            {
                result = await ExplainWithGroqAsync(prompt);
            }
            else
            {
                result = await ExplainWithGeminiAsync(prompt);
            }

            if (!result.StartsWith("Error") && !result.StartsWith("No API key") && !result.StartsWith("Could not reach"))
            {
                _cache[entry.Name] = result;
            }

            return result;
        }

        #region Groq Integration
        private static async Task<string> ExplainWithGroqAsync(string prompt)
        {
            var apiKey = LocalMachineSettingsEngine.GroqApiKey ?? Environment.GetEnvironmentVariable("GROQ_API_KEY");
            if (string.IsNullOrWhiteSpace(apiKey))
                return "No API key configured. Go to Settings to add your free Groq API key (console.groq.com).";

            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions");
                req.Headers.Add("Authorization", $"Bearer {apiKey}");
                req.Content = new StringContent(
                    JsonSerializer.Serialize(new
                    {
                        model = "llama-3.3-70b-versatile",
                        max_tokens = 300,
                        messages = new[]
                        {
                            new { role = "system", content = "You are a Windows PC expert. Explain Winapp2 cleaner entries concisely and accurately based on the file paths and registry keys provided." },
                            new { role = "user", content = prompt }
                        }
                    }),
                    Encoding.UTF8, "application/json");

                var res = await _http.SendAsync(req);
                var json = await res.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("error", out var err))
                {
                    var msg = err.TryGetProperty("message", out var m) ? m.GetString() : "Unknown error";
                    return $"Groq API error: {msg}";
                }

                return root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "No response received.";
            }
            catch (Exception ex)
            {
                return $"Could not reach Groq API: {ex.Message}";
            }
        }

        public static async Task<string> TestGroqKeyAsync(string apiKey)
        {
            if (!await NetworkHelper.IsConnectedAsync())
                return "✗ No internet connection.";

            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions");
                req.Headers.Add("Authorization", $"Bearer {apiKey}");
                req.Content = new StringContent(
                    JsonSerializer.Serialize(new
                    {
                        model = "llama-3.3-70b-versatile",
                        max_tokens = 150,
                        messages = new[] { new { role = "user", content = TestPrompt } }
                    }), Encoding.UTF8, "application/json");

                var res = await _http.SendAsync(req);
                var json = await res.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("error", out var err))
                    return "✗ " + (err.TryGetProperty("message", out var m) ? m.GetString() : "API error");

                return "✓ " + root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
            }
            catch (Exception ex) { return "✗ " + ex.Message; }
        }
        #endregion

        #region Gemini Integration
        private static async Task<string> ExplainWithGeminiAsync(string prompt)
        {
            var apiKey = LocalMachineSettingsEngine.GeminiApiKey ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");
            if (string.IsNullOrWhiteSpace(apiKey))
                return "No API key configured. Go to Settings to add your free Google Gemini API key (aistudio.google.com).";

            try
            {
                string url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={apiKey}";

                var fullPrompt = "You are a Windows PC expert. Explain Winapp2 cleaner entries concisely and accurately based on the file paths and registry keys provided.\n\n" + prompt;

                var requestBody = new { contents = new[] { new { parts = new[] { new { text = fullPrompt } } } } };
                var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

                var res = await _http.PostAsync(url, content);
                var json = await res.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("error", out var err))
                {
                    var msg = err.TryGetProperty("message", out var m) ? m.GetString() : "Unknown error";
                    return $"Gemini API error: {msg}";
                }

                return root.GetProperty("candidates")[0]
                           .GetProperty("content")
                           .GetProperty("parts")[0]
                           .GetProperty("text").GetString()?.Trim() ?? "No response received.";
            }
            catch (Exception ex)
            {
                return $"Could not reach Gemini API: {ex.Message}";
            }
        }

        public static async Task<string> TestGeminiKeyAsync(string apiKey)
        {
            if (!await NetworkHelper.IsConnectedAsync())
                return "✗ No internet connection.";

            try
            {
                string url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={apiKey}";
                var requestBody = new { contents = new[] { new { parts = new[] { new { text = TestPrompt } } } } };
                var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

                var res = await _http.PostAsync(url, content);
                var json = await res.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("error", out var err))
                    return "✗ " + (err.TryGetProperty("message", out var m) ? m.GetString() : "API error");

                var text = root.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();
                return "✓ " + text?.Trim();
            }
            catch (Exception ex) { return "✗ " + ex.Message; }
        }
        #endregion

        #region Prompt Builder
        private static string BuildPrompt(CleanerEntry entry)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Explain what the Windows cleaner entry \"{entry.Name}\" cleans and whether it is safe to delete.");

            if (!string.IsNullOrWhiteSpace(entry.Warning))
                sb.AppendLine($"Warning from the database: {entry.Warning}");

            if (entry.FileKeys.Count > 0)
            {
                sb.AppendLine("It deletes files from these locations:");
                foreach (var fk in entry.FileKeys.Take(6))
                    sb.AppendLine($"  - {fk.Path}  (pattern: {fk.Pattern})");
            }

            if (entry.RegKeys.Count > 0)
            {
                sb.AppendLine("It removes these registry keys:");
                foreach (var rk in entry.RegKeys.Take(4))
                    sb.AppendLine($"  - {rk.KeyPath}");
            }

            sb.AppendLine("Answer in 2-3 sentences. Be specific and practical.");
            return sb.ToString();
        }
        #endregion
    }
}