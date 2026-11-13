# CLAUDE.md — InstaWords («تفقيط»)

## Owner & workflow (shared rules, same in every repo)

Owner: Sarmad (`sarmadiq9090-coder`). Writes Arabic, is not technical in English, and works only
through Claude sessions, with no fixed computer.

- Reply to the owner in Arabic. Keep code, identifiers, commit messages and PR titles in English,
  and give PR bodies an Arabic summary first.
- Never push to the default branch (`main`). Work on `claude/<topic>`, open a PR, and let the owner
  merge unless they explicitly say "ادمج" (merge).
- One Claude session per repository at a time. Before starting, check open PRs and recently pushed
  `claude/*` branches. If another session is active, coordinate through it instead of editing the
  same files.
- The owner has no fixed computer: never ask them to keep files, keys or tools on a PC. Use GitHub
  (Environments/secrets, Codespaces) or their password manager.
- **Daily release train (D-035, D-047).** Each app ships once a week on its day at 09:00 Asia/Baghdad:
  Sat InstaFind, Sun Remember Me, Mon Lemon, **Tue InstaWords**, Wed InstaConvert (only when complete),
  Thu QA, Fri rest. ALL of an app's finished, green features merge together on its day (D-032/D-034);
  nothing is merged on other days. An app not ready on its day rolls to the same day next week; its
  Store update ships the same day as the release. A critical security fix may go early with the
  owner's explicit approval.
- **One GitHub writer (D-040, core/GITHUB-SAFETY.md in the brain).** Only the manager session pushes,
  opens PRs, comments, merges or dispatches workflows; other sessions work locally and report to the
  manager. Serial writes >=2 s apart, <=15 per hour, <=150 per day, <=3 new PRs per repo per day, <=2
  workflow_dispatch per repo per day, 1 rerun max. On any 403/429/abuse/CAPTCHA signal: stop all
  writes and tell the owner.
- **Closed source (D-051, 2026-11-09).** InstaWords is proprietary (free to use). Don't add "open
  source"/MIT wording; keep third-party notices (`windows/InstaWords.App/Fonts/OFL.txt`, Inno Setup).
  Versions up to 2.2.0 (published before 2026-11-09) stay under their MIT licence.
- **Signing (D-052): free tools only.** Setup and portable zip are unsigned (release notes say so);
  the Microsoft Store signs the MSIX. See `docs/CODE-SIGNING.md`.
- **Honesty.** Windows 10/11 is "not yet tested on a real device" until the owner tests
  (`docs/TESTING-CHECKLIST.md`). If the program is not sure what the user meant, it says so.
- **Mirror.** `.github/workflows/mirror.yml` pushes main, tags and claude/* daily (04:17 UTC) to GitLab
  (`fastkiwi1/instawords`), no force, no prune; the `GITLAB_DEPLOY_KEY` secret is set, so it runs daily.
- Things only the owner can do: repo settings, deleting branches or repos, money decisions, secrets,
  merging, Partner Center. Ask, and give exact click-by-click steps in Arabic.
- Don't enable features that write to third-party systems, or anything security-sensitive, without
  the owner's explicit approval.
- Commit only your own files (`git add <paths>`). Commit trailer:
  `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` (plus the session's `Claude-Session:` line).

## What this is

InstaWords writes amounts in words (tafqit) for cheques and invoices, in Arabic, English and French.
Four outputs from one repo:
- `src/`: zero-dependency TypeScript core, the source of truth (rules in `docs/SPEC.md`, golden cases
  `tests/golden/cases.json`, units in `data/units.json`).
- `site/`: offline website, built to `_site/` and published at https://fastkiwi.app/instawords/ by the
  github.io repo's daily `sync.yml` (it runs `npm run build:site` on this repo's `main`).
- `windows/`: native WPF app on .NET Framework 4.8 (`InstaWords.App`), engine `InstaWords.Core`
  (netstandard2.0) whose `Generated/*.g.cs` come from the core via `scripts/build-dotnet.mjs`.
- `vba/`: Excel module `Tafqit.bas` (generated from `Tafqit.template.bas` by `npm run build:vba`) and
  the add-in `InstaWords.xlam` (built by `scripts/build-xlam.mjs`; never opened in a real Excel yet).
Never hand-edit generated files (`windows/InstaWords.Core/Generated/`, `vba/Tafqit.bas`,
`vba/TafqitTests.bas`, committed; `src/generated/`, git-ignored): change the source and regenerate.

## Build and test

Node >= 22.18, npm. .NET 10 SDK for the C# tests and the app (the app targets net48; it builds on
any OS but runs only on Windows).

```sh
npm ci
npm test             # core + golden cases (node --test)
npm run typecheck
npm run test:vba     # VBA output == core, character for character (olevba check needs OLETOOLS_PYTHON)
npm run test:site    # Playwright smoke of _site/ (needs playwright@1.63.0 + chromium)
npm run test:all     # the three above
npm run test:dotnet  # C# engine parity (dotnet test windows/InstaWords.Core.Tests)
npm run build:dotnet # regenerate windows/InstaWords.Core/Generated/
dotnet test windows/InstaWords.App.Tests -c Release   # app unit tests (Windows CI)
```

A user-visible change gets one line under `## Unreleased` in `CHANGELOG.md` (Arabic, then English).

## CI (.github/workflows)

- `ci.yml` (every push and PR, ubuntu): typecheck, `npm test`, `test:vba` with olevba, xlam build,
  generated VBA is current, `test:dotnet`, packaging manifests follow `site/release-fallback.json`,
  `test:site`.
- `windows.yml` (every push, windows-latest): `build` (parity, generated files current, xlam, app
  unit tests, app build, UI tests with screenshots, Inno installer, portable zip), `store-package`
  (MSIX, install test, WACK), `screenshots` (main only, force-pushes the `ci-screenshots` branch),
  `release` (manual run with "release" ticked, main only).
- `mirror.yml`: daily GitLab mirror (above).

## Release (Tuesday train; details in RELEASING.md)

1. In the train PR: `npm run release:prepare -- X.Y.Z` (package.json, lock, csproj default version,
   build examples), plus a `## X.Y.Z` section in `CHANGELOG.md` (copied into the release notes;
   `tests/changelog.test.ts` and the release job fail without it). `npm run test:all`.
2. Merge to main; wait for "Windows (native app)" to pass on that commit.
3. Manager: Actions → "Windows (native app)" → Run workflow on main, tick **release**. CI tags
   `vX.Y.Z` and uploads setup, portable zip, `InstaWords.xlam`, `SHA256SUMS.txt`,
   `winget-manifests.zip`, `instawords.json`.
4. Follow-up PR: `npm run release:fallback` (site/release-fallback.json, packaging/), `test:site`.
5. winget/Scoop: `docs/PACKAGING.md`. Store: upload the `store-package` MSIX in Partner Center
   (owner), same day; each submission needs a higher version (`storeVersion` in identity.json).

## Must not break

- Asset names `InstaWords_<ver>_x64-setup.exe` and `InstaWords_<ver>_x64-portable.zip`; the updater
  matches `^InstaWords_\d+\.\d+\.\d+_x64-setup\.exe$` (`Services/UpdateLogic.cs`). Releases stay
  non-draft; pre-releases (`-rc`) are ignored by the updater.
- Update feed: `api.github.com/repos/sarmadiq9090-coder/InstaWords/releases/latest`, so the repo
  name, and asset `digest` (SHA-256) checks. Downloads only from github.com hosts in `AllowedHosts`.
- Installer AppId `{8F3B6C2A-5D4E-4A7B-9C1F-2E6D8A0B4C71}` (`InstaWords.iss` and
  `UpdateService.InstallerAppId`), mutex `Local\FastKiwi.InstaWords.Instance`, Run value `InstaWords`.
- Data folder `%APPDATA%\FastKiwi\InstaWords` (`settings.json`, `history.json`, `crash.log`;
  override `INSTAWORDS_DATA_DIR` for tests). Uninstall keeps it. Never delete user data.
- Excel: add-in file `InstaWords.xlam`, module `TafqitLib`, functions `TAFQIT` and `TAFQIT_CLEAN`.
- Store identity `FastKiwi.InstaWords` (Store ID 9P62P4WQSQNK) in `windows/packaging/msix/identity.json`,
  startup task `InstaWordsStartup`.
- `npm run build:site` must keep producing `_site/index.html` with relative paths (github.io sync).
- Core error codes and option names in README (public API of the library and the VBA function).

## Do not move or rename

`data/units.json` (+ schema), `tests/golden/cases.json`, `docs/SPEC.md`, `site/release-fallback.json`,
`vba/`, `windows/installer/InstaWords.iss`, `windows/packaging/msix/`, `packaging/` (winget, Scoop),
`scripts/` (called by workflows and npm scripts), `CHANGELOG.md` headings (`## X.Y.Z`, `## Unreleased`),
`LICENSE`, `TRADEMARKS.md`.
