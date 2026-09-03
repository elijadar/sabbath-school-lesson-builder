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

- `SabbathSchoolLessonBuilder.csproj` — single project, `net10.0`, nullable +
  implicit usings enabled.
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
dotnet build SabbathSchoolLessonBuilder.csproj
dotnet run --project SabbathSchoolLessonBuilder.csproj
```

Running performs live HTTP calls (adventech.io only) and writes `.docx` files into
the working directory — there's no offline/mock mode. Treat any run as having
network side effects and producing output files that should not be committed
(the `.gitignore` excludes generated `Суботня школа *.docx` files, but not the
tracked `Template.docx`).

## Implementing a GitHub issue

When asked to implement a feature or bug fix tracked as a GitHub issue:

1. **Read the issue** with `gh issue view <n>` (or the issue URL) to get the
   title, body, and any comments/labels — don't rely on a paraphrase, read the
   actual issue text before writing code.
2. **Branch off latest `main`** per the naming convention below, scoped to
   just that issue.
3. **Implement the change**, following the conventions/gotchas in this file.
4. **Verify**: `dotnet build SabbathSchoolLessonBuilder.csproj` must succeed;
   do a smoke run too when practical (see Build/run above).
5. **Open a PR** with `gh pr create`, referencing the issue (e.g. `Closes #8`
   in the body) so it auto-closes on merge. Do not merge it yourself unless
   explicitly told to.

## Git workflow

- **Never make changes directly on `main` and commit them.** Always create a
  feature or bug-fix branch off `main` before making any changes. This ensures
  that each unit of work is isolated and can be reviewed independently before
  merging.
- **One branch per unit of work.** Before starting a feature or bug fix,
  create a branch off the latest `main`:
  - Feature: `features/<very-short-name>` (e.g. `features/config-quarter`)
  - Bug fix: `bugs/<very-short-description>` (e.g. `bugs/getverse-crash`)
  Keep the branch scoped to one feature/bug — don't bundle unrelated changes
  onto it.
- **Always create a pull request for review** before merging any changes.
  Open the PR once the work is implemented and verified, not before. At
  minimum, confirm `dotnet build SabbathSchoolLessonBuilder.csproj` succeeds
  before opening the PR; if the change is runtime-visible and it's practical
  to do so (network access available, etc.), do a smoke run too
  (`dotnet run --project SabbathSchoolLessonBuilder.csproj`). Open the PR
  against `main` via the GitHub CLI (`gh pr create --title ... --body ...`)
  with a summary of the change and what was verified. Do not merge it
  yourself unless explicitly told to — leave it for review, and note that
  this repo does not allow plain merge commits (squash-merge only:
  `gh pr merge <n> --squash --delete-branch`).
- **After a PR merges, sync and clean up:**
  1. `git fetch --prune origin` then fast-forward local `main`:
     `git pull --ff-only origin main`.
  2. Delete the now-merged local branch. Since PRs here are squash-merged,
     git won't recognize the branch as "fully merged" by ancestry — use
     `git branch -D <branch>` once you've confirmed it's actually merged on
     GitHub (its remote ref will already be gone after `--delete-branch`).
  3. If the work was done in a separate `git worktree`, remove it:
     `git worktree remove <path> --force`, then `git worktree prune`. On
     this machine the repo lives under a OneDrive-synced folder, so removal
     can transiently fail with `Permission denied` (file lock from
     OneDrive/AV/an open editor) — retry once or twice before treating it as
     a real problem; leftover worktree directories are otherwise harmless
     and can be deleted directly once unlocked.
  4. Clean up remote branches by running `git fetch --prune origin` to
     ensure your local repository stays in sync with remote and doesn't
     accumulate stale branch references.
- `gh` (GitHub CLi) is installed on this machine but may not be on `PATH` in
  a given shell. If `gh` isn't found: in git-bash run
  `export PATH="$PATH:/c/Program Files/GitHub CLI"`; in PowerShell call it
  via full path `& "C:\Program Files\GitHub CLI\gh.exe"`.
- Independent features/bugs may be worked on in parallel (e.g. separate
  worktrees branched off the same `main` commit), but expect PRs to
  potentially conflict if they touch the same file — sequencing one after
  another is safer when in doubt. When merges do conflict, prefer merging
  `main` into the feature branch (not the reverse) to resolve it before
  merging the PR.

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
