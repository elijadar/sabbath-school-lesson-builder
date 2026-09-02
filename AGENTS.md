# AGENTS.md

Guidance for AI coding agents working in this repository.

## What this project is

A small .NET console app that generates weekly **Sabbath School lesson** study-guide
documents (in Ukrainian) as `.docx` files.

Pipeline (`WebScrapping.cs`, entry point `Program.cs`):
1. `BooksOfBible` — a static, hardcoded list of Bible book names + USFM codes
   (66 entries), used later to build per-verse deep links.
2. `GetHeaders()` — pulls one quarter's lesson data from the
   `sabbath-school-stage.adventech.io` API (`Year`/`Quarter` constants at the top of
   `WebScrapping.cs`), then for each lesson/day scrapes the HTML lesson content with
   HtmlAgilityPack to pull out the memory verse and the discussion questions
   (`<code>` blocks with `<a class="verse">` children), with a pile of regexes to
   clean up Ukrainian text and split out Bible references.
3. `CreateDocs()` — for each lesson, opens `Template.docx`, clears its body, and
   rebuilds a document (title/subtitle/headings/hyperlinked verses/questions) using
   NPOI (`XWPFDocument`), then saves it as
   `Суботня школа {year}.{quarter}.{lessonNumber}.docx` in the output directory.

`Ss.cs` holds the plain data model (`Ss`, `Day`, `Question`).
`Extensions.cs` has a `string.Change()` text-casing helper (title-case / preserve
acronyms) — currently unused by the pipeline; keep it in mind before assuming
dead code should be deleted (it may be intended for future formatting work).

There are no automated tests and no CI configuration.

## Project layout

- `WebScrapping. SS.csproj` — single project, `net10.0`, nullable + implicit usings
  enabled. Note the literal space in the project/folder name (`WebScrapping. SS`) —
  always quote paths.
- `WebScrapping.cs` — main scraping + document-generation logic (single static class).
- `Program.cs` — sets up Serilog console logging and calls `WebScrapping.Run`.
- `Extensions.cs`, `Ss.cs` — helpers / models.
- `Template.docx` — Word template copied to the output dir on build; document
  generation depends on its paragraph styles (`Title`, `Subtitle`, `Heading2`,
  `Heading3`, `Normal`, `IntenseQuote`) existing by exactly those names.
- `bin/`, `obj/`, `.vs/` — build artifacts, not source. Multiple stale TFM folders
  (`net6.0`, `net9.0`, `net10.0`) exist from earlier retargeting; only `net10.0`
  matches the current `.csproj`.

## Build / run

```powershell
dotnet build "WebScrapping. SS.csproj"
dotnet run --project "WebScrapping. SS.csproj"
```

Running performs live HTTP calls (adventech.io only) and writes `.docx` files into
the working directory — there's no offline/mock mode. Treat any run as having
network side effects and producing output files that should not be committed
(the `.gitignore` excludes generated `Суботня школа *.docx` files, but not the
tracked `Template.docx`).

## Conventions / gotchas for agents

- The app is single-purpose and imperative; everything lives in a few static
  methods on `WebScrapping`. Don't over-engineer this into layers/DI/services
  unless explicitly asked — match the existing simple, direct style.
- `Year` and `Quarter` are hardcoded consts at the top of `WebScrapping.cs` — this
  is the primary "config" for which quarter gets scraped. If asked to generate a
  different quarter, update these first.
- Text processing is tuned specifically for **Ukrainian** Sabbath School content
  (day names, verse-reference regex, string replacements like `Пам'ятний вірш`).
  Be careful editing these regexes — they encode specific Ukrainian grammatical
  patterns and known API text quirks (e.g. book name overrides for
  "Филм."→"Филимона", "Мих."→"Міхея").
- `GetBibleLink` assumes verse references have a specific `"Book Ch:Verse"` shape
  and does fragile substring/split parsing — if lesson content format changes
  upstream, this is the first place to check for breakage.
- Repo is tracked in git, hosted on GitHub as `sabbath-school-lesson-builder`
  (public). No secrets/credentials are involved.
- `adventech.io` is the official, MIT-licensed, publicly documented Sabbath School
  API run by the Adventech Ministry org — safe to call as-is (this is the same
  API their own official apps use).
- `bible.com` (YouVersion)'s Terms of Use explicitly prohibit automated/robotic
  access to their site. The app just construct plain `https://www.bible.com/uk/bible/...`
  hyperlinks for readers to click — that's fine, but do **not** introduce any
  automated HTTP call to a `bible.com`.
