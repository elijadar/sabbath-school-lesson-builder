# Sabbath School Lesson Builder

Generates Ukrainian Sabbath School lesson study guides (`.docx`) from the
[Adventech](https://adventech.io) quarterly lessons API, with clickable links to
the referenced Bible verses.

## What it does

For a given quarter, the app:

1. Downloads the quarterly index and each lesson/day's content from the
   Sabbath School API (`sabbath-school-stage.adventech.io`).
2. Parses the lesson HTML to extract the memory verse and discussion questions,
   including the Bible references embedded in them.
3. Builds one `.docx` file per lesson from `Template.docx`, with the memory verse
   and every Bible reference turned into a hyperlink (to `bible.com`).

Output files are named `Суботня школа {year}.{quarter}.{lessonNumber}.docx` and
written to the current working directory.

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Internet access (the app makes live calls to the Adventech API)

## Usage

```powershell
dotnet run --project SabbathSchoolLessonBuilder.csproj [options]
```

### CLI Parameters

| Parameter | Description | Default |
|-----------|-------------|---------|
| `--year <YYYY>` | Year for the quarter to generate (e.g., 2026) | Current year |
| `--quarter <QQ>` | Quarter number to generate (01, 02, 03, or 04) | Current quarter |
| `--week <N>` or `--week <N-M>` | Generate specific week(s): single week, multiple weeks, or range | All weeks |

### Examples

Generate the default quarter (current year/quarter):
```powershell
dotnet run --project SabbathSchoolLessonBuilder.csproj
```

Generate Q1 2025:
```powershell
dotnet run --project SabbathSchoolLessonBuilder.csproj --year 2025 --quarter 01
```

Generate Q4 2024:
```powershell
dotnet run --project SabbathSchoolLessonBuilder.csproj --year 2024 --quarter 04
```

Generate only week 1 of Q2 2026:
```powershell
dotnet run --project SabbathSchoolLessonBuilder.csproj --week 1
```

Generate weeks 2-4 (range) of Q2 2026:
```powershell
dotnet run --project SabbathSchoolLessonBuilder.csproj --week 2-4
```

Generate multiple specific weeks:
```powershell
dotnet run --project SabbathSchoolLessonBuilder.csproj --week 1 --week 3 --week 5
```

Generate Q1 2025, weeks 1-3:
```powershell
dotnet run --project SabbathSchoolLessonBuilder.csproj --year 2025 --quarter 01 --week 1-3
```

## Project structure

| File | Purpose |
|---|---|
| `Program.cs` | Entry point, sets up console logging (Serilog) |
| `WebScrapping.cs` | Fetches lesson data, parses HTML, generates `.docx` files with fluent/monadic API |
| `DocumentExtensions.cs` | Fluent extension methods for NPOI document building (paragraph chaining, hyperlinks) |
| `Ss.cs` | Data model (`Ss`, `Day`, `Question`) |
| `TextCleanup.cs` | Text processing and Bible reference extraction |
| `Extensions.cs` | Text-casing helper |
| `ApiModels.cs` | API response models for Adventech deserialization |
| `Template.docx` | Word template with styles (`Title`, `Subtitle`, `Heading2`, `Heading3`, `Normal`, `IntenseQuote`) |

## Data sources & attribution

- Lesson content comes from the [Adventech](https://github.com/Adventech) Sabbath
  School API — an official, MIT-licensed public API maintained by the Adventech
  Ministry organization for their own apps.
- Bible verse links point to [bible.com](https://www.bible.com) (YouVersion).
  This project does not call bible.com's API — it only builds plain links to it.

See [AGENTS.md](AGENTS.md) for architecture notes and conventions aimed at AI
coding agents working in this repo.

## License

[MIT](LICENSE)
