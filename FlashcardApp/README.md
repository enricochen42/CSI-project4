# Flashcard App

An AI-powered flashcard application that helps you create study flashcards from lecture slides.

## Features

- 📤 **Upload Slides**: Upload PDF files of your lecture slides
- 🤖 **AI Generation**: Automatically extract text and generate flashcards using AI
- ✏️ **Review & Edit**: Review and edit AI-generated flashcards before saving
- 📚 **Study Mode**: Study flashcards with beautiful flip animations
- 🗂️ **Deck Organization**: Organize flashcards into decks

## Setup

### 1. Install Dependencies

The project uses .NET 9.0. Make sure you have it installed, then restore packages:

```bash
dotnet restore
```

### 2. AI Configuration (Choose One)

#### Option A: Hugging Face API (Recommended for Quick Start)

1. Sign up at [Hugging Face](https://huggingface.co/)
2. Get your API token from [Settings > Access Tokens](https://huggingface.co/settings/tokens)
3. Add to `appsettings.json` or `appsettings.Development.json`:

```json
{
  "HuggingFace": {
    "ApiKey": "your-api-key-here",
    "Model": "mistralai/Mistral-7B-Instruct-v0.2"
  }
}
```

**Free Tier**: 1000 requests/month

#### Option B: Ollama (Local, Unlimited, Private)

1. Install [Ollama](https://ollama.ai/)
2. Pull a model: `ollama pull mistral` (or `llama2`, `phi3`, etc.)
3. Start Ollama (it runs on `http://localhost:11434` by default)
4. No API key needed! The app will automatically use Ollama if Hugging Face is not configured.

**Benefits**: 
- Completely free
- Unlimited usage
- Privacy - data never leaves your computer
- No internet required after setup

### 3. Run the Application

```bash
dotnet run
```

The app will be available at `https://localhost:5001` (or the port shown in the console).

## Usage

1. **Upload Slides**: Go to "Upload Slides" and select a PDF file
2. **Extract Text**: Click "Extract Text" to extract text from the PDF
3. **Generate Flashcards**: Click "Generate Flashcards with AI" to create flashcards
4. **Review & Edit**: Review the generated flashcards, edit if needed, select which to keep
5. **Save**: Choose or create a deck, then save the flashcards
6. **Study**: Go to "Study" to view and flip through your flashcards

## Database

The app uses SQLite (stored as `flashcards.db` in the project root). The database is automatically created on first run.

## Future Enhancements

- Image OCR support (for image-based PDFs)
- Spaced repetition algorithm
- Progress tracking and statistics
- Export/import flashcards
- Multiple study modes (quiz, typing, etc.)

