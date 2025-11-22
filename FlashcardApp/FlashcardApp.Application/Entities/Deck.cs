namespace FlashcardApp.Application.Entities;

public class Deck
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public List<Flashcard> Flashcards { get; set; } = new();
}

