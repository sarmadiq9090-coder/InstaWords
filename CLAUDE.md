# CLAUDE.md — InstaWords (tafket)

## Owner & workflow (shared rules, same in every repo)

Owner: Sarmad (`sarmadiq9090-coder`). Writes Arabic, is not technical in English, and works only
through Claude sessions, with no fixed computer.

- Reply to the owner in Arabic. Keep code, identifiers, commit messages and PR titles in English,
  and give PR bodies an Arabic summary first.
- Never push to the default branch. Work on `claude/<topic>`, open a PR, and let the owner merge
  unless they explicitly say "ادمج" (merge).
- One Claude session per repository at a time. Before starting, check open PRs and recently pushed
  `claude/*` branches. If another session is active, coordinate through it instead of editing the
  same files.
- The owner has no fixed computer: never ask them to keep files, keys or tools on a PC. Use GitHub
  (Environments/secrets, Codespaces) or their password manager.
- **Weekly release train.**
  - All improvements, additions and changes to a published app ship to users ONCE a week, together.
    This covers new versions and tags, website (fastkiwi.app) updates, and package-manager
    (winget/Scoop/Chocolatey) manifest updates.
  - Release day is Friday 09:00 (Asia/Baghdad). The weekly health check in the owner's manager
    session runs just before it and prepares the train; the owner approves it.
  - A release goes out only after CI is green on Linux and Windows.
  - During the week, work goes to `claude/*` branches or PRs, or is merged to the default branch
    WITHOUT bumping the version or pushing a tag.
  - arabic-file-search's build.yml publishes a release automatically when the Cargo.toml version
    changes on main, so bump the version only on release day.
  - github.io deploys on every push to main, so merge site PRs only on release day.
  - Exceptions: mawared-desktop, and new apps that have never been published. These may release
    whenever needed.
  - A critical security fix may go out early, only with the owner's explicit approval.
- Things only the owner can do: repo settings, deleting branches or repos, money decisions, secrets,
  merging. Ask, and give exact click-by-click steps in Arabic.
- Don't enable features that write to third-party systems, or anything security-sensitive, without
  the owner's explicit approval.
- Commit only your own files (`git add <paths>`).
- Commit trailer: `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## What this is

InstaWords («تفقيط», tafqit) writes amounts in words for cheques and invoices, in Arabic,
English and French. One repo, four outputs: a zero-dependency TypeScript library (`src/`), an
offline website (`site/`, live at fastkiwi.app/instawords/), a native Windows app (WPF, .NET
Framework 4.8, `windows/`) and an Excel VBA module (`vba/`). Docs: [README.md](README.md),
[CONTRIBUTING.md](CONTRIBUTING.md), [docs/SPEC.md](docs/SPEC.md), [docs/ROADMAP.md](docs/ROADMAP.md),
[windows/README.md](windows/README.md).

Default branch: **`ccr-06f5a74d-x2fvnp`** (to be renamed to `main`). Base PRs on it until then.

## Build and test

Node >= 22.18, npm. There is no lint script.

```sh
npm ci
npm run typecheck     # gen + tsc --noEmit
npm test              # gen + node --test "tests/**/*.test.ts"
npm run test:vba      # build + build:vba + VBA harness tests
npm run test:site     # build:site + Playwright smoke (npm i --no-save playwright@1.63.0, then install chromium)
npm run test:all      # test + test:vba + test:site
npm run test:dotnet   # .NET 8 SDK: generated C# is current + engine parity tests
```

Windows app (Windows only): `dotnet test windows/InstaWords.App.Tests -c Release`,
`dotnet build windows/InstaWords.App -c Release`, UI tests in `windows/InstaWords.UITests`.

## CI (.github/workflows)

- `ci.yml`, ubuntu-latest, every push and PR: typecheck, test, test:vba, generated VBA unchanged
  (`git diff --exit-code`), test:dotnet, a warning if `packaging/` is behind, test:site.
- `windows.yml`, windows-latest, every push: parity tests, generated files current, app unit tests,
  app build, UI tests with screenshots, Inno Setup installer + portable zip as artifacts. Its
  `screenshots` job force-pushes the screenshots to the `ci-screenshots` branch.

## Release

Follow [RELEASING.md](RELEASING.md). The version lives only in `package.json`
(`npm run release:prepare -- X.Y.Z`). A release is NOT a tag push: once `windows.yml` is green on the
commit, Actions → "Windows (native app)" → Run workflow → tick **release** creates tag `vX.Y.Z` and
the GitHub Release. Then `npm run release:fallback` and [docs/PACKAGING.md](docs/PACKAGING.md)
(winget/Scoop). Signing runs only if the `SIGNPATH_API_TOKEN` secret exists
([docs/CODE-SIGNING.md](docs/CODE-SIGNING.md)).

Must not break:
- Asset names `InstaWords_<version>_x64-setup.exe` and `InstaWords_<version>_x64-portable.zip`: the
  in-app updater (`windows/InstaWords.App/Services/UpdateLogic.cs`), the site and the manifests look
  for them. `SHA256SUMS.txt` is read by Scoop's autoupdate.
- The updater reads `sarmadiq9090-coder/tafket` releases (`UpdateLogic.Repo`).
- Settings folder `%APPDATA%\FastKiwi\InstaWords`; `portable.txt` marks the portable copy.
- The website: `sync.yml` in sarmadiq9090-coder.github.io rebuilds fastkiwi.app/instawords/ DAILY
  from this repo's default branch. A `site/` change merged here goes live the next day, so merge
  `site/` changes only on release day.

## Do not move or rename

- The `ci-screenshots` branch (written by `windows.yml`; never work on it or delete it).
- `data/units.json` (with `data/units.schema.json`) is the source of truth for units and currencies.
- Generated files: regenerate them, never hand-edit:
  - `src/generated/` (gitignored, `npm run gen`);
  - `vba/Tafqit.bas`, `vba/TafqitTests.bas` (`npm run build:vba`, from `vba/Tafqit.template.bas`);
  - `windows/InstaWords.Core/Generated/*.g.cs` (`npm run build:dotnet`);
  - download links and versions derived from `site/release-fallback.json` (see RELEASING.md).
- The TS core (`src/`) is the source of truth for the three engines (TypeScript, VBA, C#). For a rule
  change follow CONTRIBUTING.md: SPEC → `tests/golden/cases.json` → `src/` → VBA template → C# port,
  until `test:all` and `test:dotnet` pass.
- `packaging/` (committed winget/Scoop copies, kept in sync by `scripts/package-manifests.mjs`).

## Pending for the owner

- Rename the default branch to `main` once the Tafqit session is idle (Settings → General →
  Default branch). Then update this file.
- Repo rename `tafket` → `instawords` (ROADMAP item 14): `UpdateLogic.Repo` and github.io's
  `sync.yml` name `tafket`, so update both in the same round.
- From docs/ROADMAP.md: real-device test (docs/TESTING-CHECKLIST.md), winget/Scoop submission,
  SignPath application, deleting merged branches.
