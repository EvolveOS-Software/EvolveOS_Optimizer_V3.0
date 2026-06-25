// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Net.Http;
using System.Text;
using System.Text.Json;
using EvolveOS_Optimizer.Core.Enums;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;

namespace EvolveOS_Optimizer.Utilities.Services
{
    public static class AiExplainerService
    {
        #region Fields & Properties
        private static readonly HttpClient _http = new();
        private static readonly Dictionary<string, string> _cache = new(StringComparer.OrdinalIgnoreCase);

        public static bool IsAiReady { get; set; } = false;

        private static string TestPrompt => ResourceString.GetString("ai_explainer_test_prompt")
            ?? "Based on the following facts, describe EvolveOS Optimizer in 2 short sentences: " +
               "It is a modern, self-contained Windows management suite built with WinUI 3. " +
               "It combines aggressive system optimization, deep privacy controls, and military-grade security tools into a single lightweight executable. " +
               "Do NOT mention AI, machine learning or any AI-related features. Keep it factual.";

        private static string SystemPrompt
        {
            get
            {
                string basePrompt = ResourceString.GetString("ai_explainer_system_prompt")
                    ?? "You are a Windows PC expert. Explain system files, running processes, running services, and optimization entries concisely and accurately based on the provided context.";

                if (LocalMachineSettingsEngine.AiUseLocalization)
                {
                    string currentLanguage = System.Globalization.CultureInfo.CurrentUICulture.NativeName;
                    basePrompt += $"\n\nCRITICAL INSTRUCTION: You MUST translate your final response and answer exclusively in the following language: {currentLanguage}. Do not respond in English.";
                }

                return basePrompt;
            }
        }
        #endregion

        #region Core Service Methods
        public static void PreWarmConnection()
        {
            Task.Run(async () =>
            {
                try
                {
                    AiProvider provider = LocalMachineSettingsEngine.ActiveAiProvider;
                    string? url = provider switch
                    {
                        AiProvider.Groq => "https://api.groq.com/openai/v1/models",
                        AiProvider.OpenRouter => "https://openrouter.ai/api/v1/models",
                        AiProvider.Mistral => "https://api.mistral.ai/v1/models",
                        AiProvider.Gemini => "https://generativelanguage.googleapis.com/v1beta/models",
                        AiProvider.Cohere => "https://api.cohere.com/v1/models",
                        _ => null
                    };

                    if (url != null)
                    {
                        await _http.GetAsync(url);
                    }
                }
                catch { /* Silently ignore */ }
            });
        }

        private static async Task<string> FetchExplanationAsync(string cacheKey, string fullPrompt)
        {
            if (_cache.TryGetValue(cacheKey, out var cached))
                return cached;

            AiProvider selectedProvider = LocalMachineSettingsEngine.ActiveAiProvider;
            string result = selectedProvider switch
            {
                AiProvider.Groq => await ExplainWithGroqAsync(fullPrompt),
                AiProvider.Gemini => await ExplainWithGeminiAsync(fullPrompt),
                AiProvider.OpenRouter => await ExplainWithOpenRouterAsync(fullPrompt),
                AiProvider.Cohere => await ExplainWithCohereAsync(fullPrompt),
                AiProvider.Mistral => await ExplainWithMistralAsync(fullPrompt),
                _ => ResourceString.GetString("ai_err_invalid_provider") ?? "Selected AI provider is not supported."
            };

            if (!result.StartsWith(ResourceString.GetString("ai_err_prefix") ?? "Error") &&
                !result.Contains("API key") &&
                !result.Contains("Could not reach"))
            {
                _cache[cacheKey] = result;
            }

            return result;
        }

        public static async Task<string> ExplainAsync(CleanerEntry entry)
        {
            string prompt = BuildPrompt(entry);
            return await FetchExplanationAsync(entry.Name, prompt);
        }

        public static async Task<string> ExplainGenericItemAsync(string itemName, string itemCategory, string contextDetails = "")
        {
            var sb = new StringBuilder();

            sb.AppendLine($"Explain the purpose of the {itemCategory} named \"{itemName}\" and whether it is safe to modify, disable, or delete.");

            if (!string.IsNullOrWhiteSpace(contextDetails))
            {
                sb.AppendLine("Context/Details:");
                sb.AppendLine(contextDetails);
            }

            sb.AppendLine(ResourceString.GetString("ai_explainer_prompt_end") ?? "Answer in 2-3 sentences. Be specific and practical.");

            return await FetchExplanationAsync($"{itemCategory}_{itemName}", sb.ToString());
        }
        #endregion

        #region Groq Integration
        private static async Task<string> ExplainWithGroqAsync(string prompt)
        {
            var apiKey = LocalMachineSettingsEngine.GroqApiKey ?? Environment.GetEnvironmentVariable("GROQ_API_KEY");
            if (string.IsNullOrWhiteSpace(apiKey))
                return ResourceString.GetString("ai_err_no_key_groq") ?? "No API key configured. Go to Settings to add your free Groq API key.";

            return await ExecuteOpenAiCompatibleRequestAsync("https://api.groq.com/openai/v1/chat/completions", apiKey, "llama-3.3-70b-versatile", prompt);
        }

        public static async Task<string> TestGroqKeyAsync(string apiKey) =>
            await TestOpenAiCompatibleKeyAsync("https://api.groq.com/openai/v1/chat/completions", apiKey, "llama-3.3-70b-versatile");
        #endregion

        #region OpenRouter Integration
        private static async Task<string> ExplainWithOpenRouterAsync(string prompt)
        {
            var apiKey = LocalMachineSettingsEngine.OpenRouterApiKey ?? Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
            if (string.IsNullOrWhiteSpace(apiKey))
                return ResourceString.GetString("ai_err_no_key_openrouter") ?? "No API key configured. Go to Settings to add your free OpenRouter API key.";

            return await ExecuteOpenAiCompatibleRequestAsync("https://openrouter.ai/api/v1/chat/completions", apiKey, "meta-llama/llama-3-8b-instruct:free", prompt);
        }

        public static async Task<string> TestOpenRouterKeyAsync(string apiKey) =>
            await TestOpenAiCompatibleKeyAsync("https://openrouter.ai/api/v1/chat/completions", apiKey, "meta-llama/llama-3-8b-instruct:free");
        #endregion

        #region Mistral AI Integration
        private static async Task<string> ExplainWithMistralAsync(string prompt)
        {
            var apiKey = LocalMachineSettingsEngine.MistralApiKey ?? Environment.GetEnvironmentVariable("MISTRAL_API_KEY");
            if (string.IsNullOrWhiteSpace(apiKey))
                return ResourceString.GetString("ai_err_no_key_mistral") ?? "No API key configured. Go to Settings to add your Mistral API key.";

            return await ExecuteOpenAiCompatibleRequestAsync("https://api.mistral.ai/v1/chat/completions", apiKey, "open-mistral-7b", prompt);
        }

        public static async Task<string> TestMistralKeyAsync(string apiKey) =>
            await TestOpenAiCompatibleKeyAsync("https://api.mistral.ai/v1/chat/completions", apiKey, "open-mistral-7b");
        #endregion

        #region Cohere Integration
        private static async Task<string> ExplainWithCohereAsync(string prompt)
        {
            var apiKey = LocalMachineSettingsEngine.CohereApiKey ?? Environment.GetEnvironmentVariable("COHERE_API_KEY");
            if (string.IsNullOrWhiteSpace(apiKey))
                return ResourceString.GetString("ai_err_no_key_cohere") ?? "No API key configured. Go to Settings to add your free Cohere Trial API key.";

            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.cohere.com/v1/chat");
                req.Headers.Add("Authorization", $"Bearer {apiKey}");
                req.Headers.Add("Accept", "application/json");

                req.Content = new StringContent(JsonSerializer.Serialize(new
                {
                    model = "command-light",
                    message = prompt,
                    preamble = SystemPrompt,
                    max_tokens = 300
                }), Encoding.UTF8, "application/json");

                var res = await _http.SendAsync(req);
                var json = await res.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("message", out var err))
                    return $"{ResourceString.GetString("ai_err_prefix")} Cohere: {err.GetString()}";

                return root.GetProperty("text").GetString() ?? ResourceString.GetString("ai_err_no_response");
            }
            catch (Exception ex)
            {
                return $"{ResourceString.GetString("ai_err_network")} Cohere: {ex.Message}";
            }
        }

        public static async Task<string> TestCohereKeyAsync(string apiKey)
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.cohere.com/v1/chat");
                req.Headers.Add("Authorization", $"Bearer {apiKey}");
                req.Headers.Add("Accept", "application/json");

                req.Content = new StringContent(JsonSerializer.Serialize(new
                {
                    model = "command-light",
                    message = TestPrompt,
                    max_tokens = 150
                }), Encoding.UTF8, "application/json");

                var res = await _http.SendAsync(req);
                var json = await res.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("message", out var err))
                    return "✗ " + err.GetString();

                return "✓ " + root.GetProperty("text").GetString()?.Trim();
            }
            catch (Exception ex) { return "✗ " + ex.Message; }
        }
        #endregion

        #region Gemini Integration
        private static async Task<string> ExplainWithGeminiAsync(string prompt)
        {
            var apiKey = LocalMachineSettingsEngine.GeminiApiKey ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");
            if (string.IsNullOrWhiteSpace(apiKey))
                return ResourceString.GetString("ai_err_no_key_gemini") ?? "No API key configured. Go to Settings to add your free Google Gemini API key.";

            try
            {
                string url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={apiKey}";
                var fullPrompt = SystemPrompt + "\n\n" + prompt;
                var requestBody = new { contents = new[] { new { parts = new[] { new { text = fullPrompt } } } } };
                var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

                var res = await _http.PostAsync(url, content);
                var json = await res.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("error", out var err))
                {
                    var msg = err.TryGetProperty("message", out var m) ? m.GetString() : ResourceString.GetString("ai_err_unknown");
                    return $"{ResourceString.GetString("ai_err_prefix")} Gemini: {msg}";
                }

                return root.GetProperty("candidates")[0]
                           .GetProperty("content")
                           .GetProperty("parts")[0]
                           .GetProperty("text").GetString()?.Trim() ?? ResourceString.GetString("ai_err_no_response");
            }
            catch (Exception ex)
            {
                return $"{ResourceString.GetString("ai_err_network")} Gemini: {ex.Message}";
            }
        }

        public static async Task<string> TestGeminiKeyAsync(string apiKey)
        {
            try
            {
                string url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={apiKey}";
                var requestBody = new { contents = new[] { new { parts = new[] { new { text = TestPrompt } } } } };
                var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

                var res = await _http.PostAsync(url, content);
                var json = await res.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("error", out var err))
                    return "✗ " + (err.TryGetProperty("message", out var m) ? m.GetString() : ResourceString.GetString("ai_err_unknown"));

                var text = root.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();
                return "✓ " + text?.Trim();
            }
            catch (Exception ex) { return "✗ " + ex.Message; }
        }
        #endregion

        #region Shared OpenAI-Compatible Helpers (Groq, OpenRouter, Mistral)
        private static async Task<string> ExecuteOpenAiCompatibleRequestAsync(string endpoint, string apiKey, string model, string prompt)
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, endpoint);
                req.Headers.Add("Authorization", $"Bearer {apiKey}");
                req.Content = new StringContent(JsonSerializer.Serialize(new
                {
                    model = model,
                    max_tokens = 300,
                    messages = new[]
                    {
                        new { role = "system", content = SystemPrompt },
                        new { role = "user", content = prompt }
                    }
                }), Encoding.UTF8, "application/json");

                var res = await _http.SendAsync(req);
                var json = await res.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("error", out var err))
                {
                    var msg = err.TryGetProperty("message", out var m) ? m.GetString() : ResourceString.GetString("ai_err_unknown");
                    return $"{ResourceString.GetString("ai_err_prefix")}: {msg}";
                }

                return root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? ResourceString.GetString("ai_err_no_response");
            }
            catch (Exception ex)
            {
                return $"{ResourceString.GetString("ai_err_network")}: {ex.Message}";
            }
        }

        private static async Task<string> TestOpenAiCompatibleKeyAsync(string endpoint, string apiKey, string model)
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, endpoint);
                req.Headers.Add("Authorization", $"Bearer {apiKey}");
                req.Content = new StringContent(JsonSerializer.Serialize(new
                {
                    model = model,
                    max_tokens = 150,
                    messages = new[] { new { role = "user", content = TestPrompt } }
                }), Encoding.UTF8, "application/json");

                var res = await _http.SendAsync(req);
                var json = await res.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("error", out var err))
                    return "✗ " + (err.TryGetProperty("message", out var m) ? m.GetString() : ResourceString.GetString("ai_err_unknown"));

                return "✓ " + root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
            }
            catch (Exception ex) { return "✗ " + ex.Message; }
        }
        #endregion

        #region Prompt Builder
        private static string BuildPrompt(CleanerEntry entry)
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Format(ResourceString.GetString("ai_explainer_prompt_start") ?? "Explain what the Windows cleaner entry \"{0}\" cleans and whether it is safe to delete.", entry.Name));

            if (!string.IsNullOrWhiteSpace(entry.Warning))
                sb.AppendLine($"{ResourceString.GetString("ai_explainer_prompt_warning") ?? "Warning from the database:"} {entry.Warning}");

            if (entry.FileKeys.Count > 0)
            {
                sb.AppendLine(ResourceString.GetString("ai_explainer_prompt_files") ?? "It deletes files from these locations:");
                foreach (var fk in entry.FileKeys.Take(6))
                    sb.AppendLine($"  - {fk.Path}  (pattern: {fk.Pattern})");
            }

            if (entry.RegKeys.Count > 0)
            {
                sb.AppendLine(ResourceString.GetString("ai_explainer_prompt_registry") ?? "It removes these registry keys:");
                foreach (var rk in entry.RegKeys.Take(4))
                    sb.AppendLine($"  - {rk.KeyPath}");
            }

            sb.AppendLine(ResourceString.GetString("ai_explainer_prompt_end") ?? "Answer in 2-3 sentences. Be specific and practical.");
            return sb.ToString();
        }
        #endregion
    }
}