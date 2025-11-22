using System.Text;
using System.Text.Json;
using FlashcardApp.Application.DTOs;
using FlashcardApp.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FlashcardApp.Infrastructure.Services;

public class AIGenerationService : IAIGenerationService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AIGenerationService> _logger;

    public AIGenerationService(HttpClient httpClient, IConfiguration configuration, ILogger<AIGenerationService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<List<GeneratedFlashcardDto>> GenerateFlashcardsAsync(string textContent)
    {
        try
        {
            // Try Groq first (free, fast, reliable)
            // Check both configuration formats: "Groq:ApiKey" (appsettings/User Secrets) and "Groq__ApiKey" (environment variable)
            var groqApiKey = _configuration["Groq:ApiKey"] ?? _configuration["Groq__ApiKey"] ?? "";
            
            // Debug: Log if API key is found (without exposing the key)
            if (string.IsNullOrEmpty(groqApiKey))
            {
                _logger.LogWarning("Groq API key not found in configuration. Checked: Groq:ApiKey and Groq__ApiKey");
            }
            else
            {
                _logger.LogInformation("Groq API key found (length: {Length})", groqApiKey.Length);
            }
            
            if (!string.IsNullOrEmpty(groqApiKey))
            {
                try
                {
                    var model = _configuration["Groq:Model"] ?? "llama-3.1-8b-instant";
                    return await GenerateWithGroqAsync(textContent, groqApiKey, model);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Groq generation failed, trying fallback");
                    // Fall through to try other options
                }
            }

            // Try HuggingFace next
            var huggingFaceApiKey = _configuration["HuggingFace:ApiKey"] ?? "";
            if (!string.IsNullOrEmpty(huggingFaceApiKey))
            {
                try
                {
                    var model = _configuration["HuggingFace:Model"] ?? "mistralai/Mistral-7B-Instruct-v0.2";
                    return await GenerateWithHuggingFaceAsync(textContent, huggingFaceApiKey, model);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "HuggingFace generation failed");
                    throw new Exception("Hugging Face API error. Please check your API key or try Groq instead.");
                }
            }

            // No API keys configured - give clear error message
            throw new Exception(
                "No AI API key configured. " +
                "Please set your Groq API key using one of these methods:\n" +
                "1. Environment variable: $env:Groq__ApiKey = 'your-key-here' (PowerShell) or export Groq__ApiKey='your-key-here' (bash)\n" +
                "2. User Secrets: dotnet user-secrets set \"Groq:ApiKey\" \"your-key-here\"\n" +
                "3. appsettings.Development.json: Add your API key to the Groq section\n" +
                "\nGet your free API key at: https://console.groq.com/"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating flashcards with AI");
            throw;
        }
    }

    private async Task<List<GeneratedFlashcardDto>> GenerateWithGroqAsync(string textContent, string apiKey, string model)
    {
        var prompt = BuildPrompt(textContent);
        
        var requestBody = new
        {
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = prompt
                }
            },
            model = model,
            temperature = 0.7,
            max_tokens = 4000,
            top_p = 1,
            stream = false
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

        var url = "https://api.groq.com/openai/v1/chat/completions";
        var response = await _httpClient.PostAsync(url, content);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new Exception($"Groq API error: {response.StatusCode} - {errorContent}");
        }

        var responseContent = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<GroqResponse>(responseContent);

        if (result == null || result.choices == null || result.choices.Length == 0)
        {
            throw new Exception("No response from Groq API");
        }

        var generatedText = result.choices[0].message?.content ?? "";
        return ParseFlashcards(generatedText);
    }

    private async Task<List<GeneratedFlashcardDto>> GenerateWithHuggingFaceAsync(string textContent, string apiKey, string model)
    {
        var prompt = BuildPrompt(textContent);
        
        var requestBody = new
        {
            inputs = prompt,
            parameters = new
            {
                max_new_tokens = 2000,
                temperature = 0.7,
                return_full_text = false
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

        var url = $"https://api-inference.huggingface.co/models/{model}";
        var response = await _httpClient.PostAsync(url, content);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new Exception($"Hugging Face API error: {response.StatusCode} - {errorContent}");
        }

        var responseContent = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<HuggingFaceResponse[]>(responseContent);

        if (result == null || result.Length == 0)
        {
            throw new Exception("No response from AI service");
        }

        var generatedText = result[0].generated_text ?? "";
        return ParseFlashcards(generatedText);
    }


    private string BuildPrompt(string textContent)
    {
        // For Groq and modern APIs, we can send more text (up to ~8000 chars for better context)
        // For older APIs, limit to 3000
        var limitedText = textContent.Length > 8000 
            ? textContent.Substring(0, 8000) + "\n\n[Content truncated due to length...]" 
            : textContent;

        return $@"From the following lecture content, generate 5-10 flashcards suitable for oral exam preparation.

For each flashcard, format it EXACTLY like this:
QUESTION: [your question here]
ANSWER: [your detailed answer here]
---

Focus on:
- Key concepts and definitions
- Important facts and relationships
- Topics that would be asked in an oral exam
- Clear, concise questions
- Detailed, comprehensive answers

Lecture Content:
{limitedText}

Generate the flashcards now:";
    }

    private List<GeneratedFlashcardDto> ParseFlashcards(string aiResponse)
    {
        var flashcards = new List<GeneratedFlashcardDto>();
        
        var sections = aiResponse.Split(new[] { "---" }, StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var section in sections)
        {
            var lines = section.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            string? question = null;
            var answerLines = new List<string>();

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("QUESTION:", StringComparison.OrdinalIgnoreCase))
                {
                    question = trimmed.Substring("QUESTION:".Length).Trim();
                }
                else if (trimmed.StartsWith("ANSWER:", StringComparison.OrdinalIgnoreCase))
                {
                    answerLines.Add(trimmed.Substring("ANSWER:".Length).Trim());
                }
                else if (!string.IsNullOrEmpty(trimmed) && answerLines.Count > 0)
                {
                    answerLines.Add(trimmed);
                }
                else if (!string.IsNullOrEmpty(question) && answerLines.Count == 0)
                {
                    if (question.Length < 100)
                    {
                        question += " " + trimmed;
                    }
                }
            }

            var answer = string.Join("\n", answerLines).Trim();

            if (!string.IsNullOrEmpty(question) && !string.IsNullOrEmpty(answer))
            {
                flashcards.Add(new GeneratedFlashcardDto
                {
                    Question = question,
                    Answer = answer
                });
            }
        }

        if (flashcards.Count == 0)
        {
            flashcards = ParseAlternativeFormat(aiResponse);
        }

        return flashcards;
    }

    private List<GeneratedFlashcardDto> ParseAlternativeFormat(string aiResponse)
    {
        var flashcards = new List<GeneratedFlashcardDto>();
        
        var parts = aiResponse.Split(new[] { "Q:", "Question:", "A:", "Answer:" }, StringSplitOptions.RemoveEmptyEntries);
        
        for (int i = 0; i < parts.Length - 1; i += 2)
        {
            var question = parts[i].Trim();
            var answer = parts[i + 1].Trim();
            
            if (!string.IsNullOrEmpty(question) && !string.IsNullOrEmpty(answer))
            {
                flashcards.Add(new GeneratedFlashcardDto
                {
                    Question = question,
                    Answer = answer
                });
            }
        }

        return flashcards;
    }
}

internal class HuggingFaceResponse
{
    public string? generated_text { get; set; }
}

internal class GroqResponse
{
    public GroqChoice[]? choices { get; set; }
}

internal class GroqChoice
{
    public GroqMessage? message { get; set; }
}

internal class GroqMessage
{
    public string? content { get; set; }
}

