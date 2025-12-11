using FlashcardApp.Application.Interfaces;
using FlashcardApp.Application.Entities;
using Microsoft.AspNetCore.Mvc;

namespace FlashcardApp.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class DecksController : ControllerBase
{
    private readonly IFlashcardService _flashcardService;
    private readonly ILogger<DecksController> _logger;

    public DecksController(IFlashcardService flashcardService, ILogger<DecksController> logger)
    {
        _flashcardService = flashcardService;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<Deck>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<Deck>>> GetAllDecks()
    {
        try
        {
            var decks = await _flashcardService.GetAllDecksAsync();
            return Ok(decks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all decks");
            return StatusCode(500, "An error occurred while retrieving decks");
        }
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(Deck), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Deck>> GetDeck(int id)
    {
        try
        {
            var deck = await _flashcardService.GetDeckByIdAsync(id);
            
            if (deck == null)
            {
                return NotFound($"Deck with ID {id} not found");
            }

            return Ok(deck);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting deck {DeckId}", id);
            return StatusCode(500, "An error occurred while retrieving the deck");
        }
    }

    [HttpPost]
    [ProducesResponseType(typeof(Deck), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Deck>> CreateDeck([FromBody] CreateDeckRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest("Deck name is required");
            }

            var deck = await _flashcardService.CreateDeckAsync(request.Name, request.Description);
            
            return CreatedAtAction(
                nameof(GetDeck),
                new { id = deck.Id },
                deck);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating deck");
            return StatusCode(500, "An error occurred while creating the deck");
        }
    }

    [HttpGet("{id}/flashcards")]
    [ProducesResponseType(typeof(IEnumerable<Flashcard>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<Flashcard>>> GetDeckFlashcards(int id)
    {
        try
        {
            var deck = await _flashcardService.GetDeckByIdAsync(id);
            if (deck == null)
            {
                return NotFound($"Deck with ID {id} not found");
            }

            var flashcards = await _flashcardService.GetFlashcardsByDeckIdAsync(id);
            return Ok(flashcards);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting flashcards for deck {DeckId}", id);
            return StatusCode(500, "An error occurred while retrieving flashcards");
        }
    }

    [HttpPost("{id}/flashcards")]
    [ProducesResponseType(typeof(Flashcard), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Flashcard>> CreateFlashcard(int id, [FromBody] CreateFlashcardRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Question))
            {
                return BadRequest("Question is required");
            }

            if (string.IsNullOrWhiteSpace(request.Answer))
            {
                return BadRequest("Answer is required");
            }

            var deck = await _flashcardService.GetDeckByIdAsync(id);
            if (deck == null)
            {
                return NotFound($"Deck with ID {id} not found");
            }

            var flashcard = await _flashcardService.CreateFlashcardAsync(id, request.Question, request.Answer);
            
            return CreatedAtAction(
                nameof(GetDeckFlashcards),
                new { id = deck.Id },
                flashcard);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating flashcard in deck {DeckId}", id);
            return StatusCode(500, "An error occurred while creating the flashcard");
        }
    }
}

public record CreateDeckRequest(string Name, string? Description = null);
public record CreateFlashcardRequest(string Question, string Answer);

