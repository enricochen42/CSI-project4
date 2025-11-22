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
            var apiKey = _configuration["HuggingFace:ApiKey"] ?? "";
            var model = _configuration["HuggingFace:Model"] ?? "mistralai/Mistral-7B-Instruct-v0.2";

            if (string.IsNullOrEmpty(apiKey))
            {
                return await GenerateWithOllamaAsync(textContent);
            }

            return await GenerateWithHuggingFaceAsync(textContent, apiKey, model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating flashcards with AI");
            throw;
        }
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

    private async Task<List<GeneratedFlashcardDto>> GenerateWithOllamaAsync(string textContent)
    {
        var prompt = BuildPrompt(textContent);
        
        var requestBody = new
        {
            model = "mistral",
            prompt = prompt,
            stream = false
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        _httpClient.DefaultRequestHeaders.Clear();

        var url = "http://localhost:11434/api/generate";
        var response = await _httpClient.PostAsync(url, content);

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Ollama not available. Please install Ollama and start it, or configure Hugging Face API key.");
        }

        var responseContent = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<OllamaResponse>(responseContent);

        if (result == null || string.IsNullOrEmpty(result.response))
        {
            throw new Exception("No response from Ollama");
        }

        return ParseFlashcards(result.response);
    }

    private string BuildPrompt(string textContent)
    {
        var limitedText = textContent.Length > 3000 ? textContent.Substring(0, 3000) + "..." : textContent;

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

internal class OllamaResponse
{
    public string response { get; set; } = string.Empty;
}

