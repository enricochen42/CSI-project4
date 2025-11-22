namespace FlashcardApp.Application.Entities;

public class Flashcard
{
    public int Id { get; set; }
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public int DeckId { get; set; }
    public Deck? Deck { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}

