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

    private const int MaxFlashcards = 20;

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
                throw new Exception(
                    "No Groq API key configured. " +
                    "Please set it using one of these methods:\n" +
                    "1. Environment variable: $env:Groq__ApiKey = 'your-key-here' (PowerShell) or export Groq__ApiKey='your-key-here' (bash)\n" +
                    "2. User Secrets: dotnet user-secrets set \"Groq:ApiKey\" \"your-key-here\"\n" +
                    "3. appsettings.Development.json: Add your API key to the Groq section\n" +
                    "\nGet your free API key at: https://console.groq.com/"
                );
            }

            var model = _configuration["Groq:Model"] ?? "llama-3.1-8b-instant";
            try
            {
                return await GenerateWithGroqAsync(textContent, groqApiKey, model);
            }
            catch (Exception ex)
            {
                // If it's a rate limit error or any other Groq failure, throw exception
                if (ex.Message.Contains("TooManyRequests") || ex.Message.Contains("rate_limit_exceeded"))
                {
                    throw new Exception("Groq API rate limit exceeded. Please wait and try again later.", ex);
                }

                throw new Exception($"Groq generation failed: {ex.Message}", ex);
            }
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
        _logger.LogInformation("AI response: {0}\n", generatedText);

        return ParseFlashcards(generatedText);
    }

    private string BuildPrompt(string textContent)
    {
        // For Groq and modern APIs, we can send more text (up to ~8000 chars for better context)
        // For older APIs, limit to 3000
        var limitedText = textContent.Length > 8000 
            ? textContent.Substring(0, 8000) + "\n\n[Content truncated due to length...]" 
            : textContent;

     return $@"From the following lecture content, generate 10-15 flashcards suitable for oral exam preparation.

            For each flashcard, format it EXACTLY like this and separate each flashcard with <<<END>>> (three angle brackets, END, three angle brackets):
            QUESTION: [your question here]
            ANSWER: [your detailed answer here]
            <<<END>>>

            Focus on:
            - Key concepts and definitions
            - Important facts and relationships
            - Topics that would be asked in an oral exam
            - Clear, concise questions
            - Detailed, comprehensive answers

            Lecture Content:
            {limitedText}

            Generate the flashcards now and DO NOT add any other commentary. Use <<<END>>> between flashcards only.";
    }

    // Robust parser for AI responses that extracts QUESTION/ANSWER pairs.
    // It prefers an explicit separator <<<END>>> but falls back to tolerant line-by-line parsing
    // that handles numbering (e.g. "1.", "1)", "**1.") and prefixes like "QUESTION:", "Q:", "ANSWER:", "A:".
    private List<GeneratedFlashcardDto> ParseFlashcards(string aiResponse)
    {
        var flashcards = new List<GeneratedFlashcardDto>();

        if (string.IsNullOrWhiteSpace(aiResponse))
            return flashcards;

        // 1) First attempt: explicit separator <<<END>>> (most reliable)
        if (aiResponse.Contains("<<<END>>>"))
        {
            var sections = aiResponse.Split(new[] { "<<<END>>>" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var section in sections)
            {
                var (q, a) = ExtractQuestionAnswer(section);
                if (!string.IsNullOrEmpty(q) && !string.IsNullOrEmpty(a))
                {
                    flashcards.Add(new GeneratedFlashcardDto { Question = q, Answer = a });
                }
            }

            if (flashcards.Count > 0)
                return flashcards // return early if we found valid flashcards
                    .Take(MaxFlashcards)
                    .ToList();
        }

        // 2) Fallback: tolerant line-by-line parsing
        var normalized = RegexNormalizeResponse(aiResponse);
        var lines = normalized.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

        string? currentQuestion = null;
        var currentAnswerLines = new List<string>();
        bool inAnswer = false;

        foreach (var raw in lines)
        {
            var line = raw.Trim();

            // Detect question/answer keywords (case-insensitive)
            var idxQuestion = IndexOfKeyword(line, "QUESTION:");
            var idxQshort = IndexOfKeyword(line, "Q:");
            var idxAnswer = IndexOfKeyword(line, "ANSWER:");
            var idxAshort = IndexOfKeyword(line, "A:");

            if (idxQuestion >= 0 || idxQshort >= 0)
            {
                // If there is a pending Q/A, save it before starting a new one
                if (!string.IsNullOrEmpty(currentQuestion) && currentAnswerLines.Count > 0)
                {
                    flashcards.Add(new GeneratedFlashcardDto
                    {
                        Question = currentQuestion.Trim(),
                        Answer = string.Join("\n", currentAnswerLines).Trim()
                    });
                }

                // Reset state for the new card
                currentAnswerLines.Clear();
                inAnswer = false;

                var content = idxQuestion >= 0
                    ? line.Substring(idxQuestion + "QUESTION:".Length).Trim()
                    : line.Substring(idxQshort + "Q:".Length).Trim();

                currentQuestion = content;
                continue;
            }

            if (idxAnswer >= 0 || idxAshort >= 0)
            {
                inAnswer = true;
                var content = idxAnswer >= 0
                    ? line.Substring(idxAnswer + "ANSWER:".Length).Trim()
                    : line.Substring(idxAshort + "A:".Length).Trim();

                if (!string.IsNullOrEmpty(content))
                    currentAnswerLines.Add(content);

                continue;
            }

            // If the line is neither QUESTION nor ANSWER, attach it to the current context
            if (inAnswer)
            {
                currentAnswerLines.Add(line);
            }
            else if (!string.IsNullOrEmpty(currentQuestion) && currentAnswerLines.Count == 0)
            {
                // Possibly a continuation of a long question
                if (currentQuestion.Length < 200)
                {
                    currentQuestion += " " + line;
                }
                else
                {
                    // If question is already long, treat the line as answer content
                    currentAnswerLines.Add(line);
                }
            }
        }

        // Add the last accumulated flashcard if valid
        if (!string.IsNullOrEmpty(currentQuestion) && currentAnswerLines.Count > 0)
        {
            flashcards.Add(new GeneratedFlashcardDto
            {
                Question = currentQuestion.Trim(),
                Answer = string.Join("\n", currentAnswerLines).Trim()
            });
        }

        return flashcards
            .Take(MaxFlashcards)
            .ToList();
    }

    // Helper: normalize response lines by removing common leading numbering/markers
    private string RegexNormalizeResponse(string s)
    {
        var lines = s.Split(new[] { '\n' }, StringSplitOptions.None);
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimStart();

            // Remove bold markers like "**" at the start
            line = System.Text.RegularExpressions.Regex.Replace(line, @"^\*+\s*", "");

            // Remove numeric prefixes like "1. ", "1) "
            line = System.Text.RegularExpressions.Regex.Replace(line, @"^\d+[\.\)]\s*", "");

            // Remove common list bullets like "-", "*", "•", "+"
            line = System.Text.RegularExpressions.Regex.Replace(line, @"^[\-\*\•\+]\s*", "");

            lines[i] = line;
        }
        return string.Join("\n", lines);
    }

    // Helper: case-insensitive index search for a keyword; returns -1 if not found
    private int IndexOfKeyword(string line, string keyword)
    {
        return line.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
    }

    // Helper: extract question and answer from a single section block (used when <<<END>>> is present)
    // The function tolerates "QUESTION:", "Q:", "ANSWER:", "A:" and multi-line answers/questions.
    private (string? question, string? answer) ExtractQuestionAnswer(string section)
    {
        var lines = section.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        string? question = null;
        var answerLines = new List<string>();
        bool inAnswer = false;

        foreach (var raw in lines)
        {
            var line = raw.Trim();

            if (line.StartsWith("QUESTION:", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("Q:", StringComparison.OrdinalIgnoreCase))
            {
                var idx = line.IndexOf(':');
                question = line.Substring(idx + 1).Trim();
                continue;
            }

            if (line.StartsWith("ANSWER:", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("A:", StringComparison.OrdinalIgnoreCase))
            {
                inAnswer = true;
                var idx = line.IndexOf(':');
                var after = line.Substring(idx + 1).Trim();
                if (!string.IsNullOrEmpty(after))
                    answerLines.Add(after);
                continue;
            }

            if (inAnswer && !string.IsNullOrWhiteSpace(line))
            {
                answerLines.Add(line);
            }
            else if (!inAnswer && !string.IsNullOrEmpty(question) && answerLines.Count == 0)
            {
                // Treat as continuation of the question if answer hasn't started yet
                if (question.Length < 200)
                    question += " " + line;
            }
        }

        return (question?.Trim(), string.Join("\n", answerLines).Trim());
    }
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

