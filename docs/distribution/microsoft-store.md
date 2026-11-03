# InstaWords in the Microsoft Store · النشر في متجر Microsoft

<div dir="rtl">

**الخلاصة بالعربية:** برنامج InstaWords الأصلي (WPF) يُعبّأ حزمة **MSIX** يبنيها GitHub Actions مع كل إيداع، باسم الملف المؤقت `store-package`. الحزمة غير موقّعة لأن المتجر يوقّعها بنفسه، وهوية Partner Center الحقيقية موجودة في `windows/packaging/msix/identity.json`. في نسخة المتجر لا يتحقق البرنامج من التحديثات ولا ينزّل شيئاً، ويعرض بدلاً من ذلك «التحديثات تأتي من Microsoft Store». ويعمل «التشغيل مع Windows» عبر مهمة بدء التشغيل الخاصة بالحزمة. نصوص صفحة المتجر بالعربية والإنجليزية، وقائمة خطوات الإرسال، في الأقسام أدناه.

</div>

| | |
|---|---|
| Store ID | **9P62P4WQSQNK** |
| Store page (after it is published) | https://apps.microsoft.com/detail/9P62P4WQSQNK · `ms-windows-store://pdp/?ProductId=9P62P4WQSQNK` |
| Package/Identity/Name | `FastKiwi.InstaWords` |
| Package/Identity/Publisher | `CN=8EFD6949-D088-481F-886D-82300679C5E5` |
| Package/Properties/PublisherDisplayName | `FastKiwi` |
| Package family name | `FastKiwi.InstaWords_sm1xjmj6v76yc` (CI checks the installed test copy against it) |
| Privacy policy | https://fastkiwi.app/privacy/ |
| Package file | `InstaWords_<x.y.z>.0_x64.msix`, workflow artifact **`store-package`** |

## 1. How the package is built

**Approach: a hand-written `AppxManifest.xml` template, packed with MakePri and MakeAppx from the Windows SDK. No `.wapproj` is used.**
Why:

- A `.wapproj` (Windows Application Packaging Project) needs Visual Studio's Desktop Bridge MSBuild targets. `dotnet build` does not have them, so the solution would no longer build on Linux, where CI and contributors build `InstaWords.sln` today. The wapproj would also add its own signing and bundling defaults to work around.
- MakeAppx and MakePri come with the Windows SDK on every `windows-latest` runner. The only input is a folder, which is easy to inspect, and the output is deterministic.
- The manifest is filled by a small Node script (`scripts/msix-stage.mjs` + `scripts/lib/msix.ts`), which the repository's normal tests cover (`tests/msix.test.ts`, run by `npm test` on Linux).

Files, in `windows/packaging/msix/`:

| File | What it is |
|---|---|
| `AppxManifest.xml` | Template: `Windows.Desktop` MinVersion **10.0.19041.0** (Windows 10 2004, the first version with .NET Framework 4.8 built in), MaxVersionTested 10.0.26100.0, `x64`, `runFullTrust`, `EntryPoint="Windows.FullTrustApplication"`, a `uap5:StartupTask` (off by default), resources **en-US** and **ar**. `{{Name}}`, `{{Publisher}}`, `{{PublisherDisplayName}}` and `{{Version}}` are filled in at build time. |
| `identity.json` | The Partner Center identity (table above). `"placeholder": true` (or the test publisher `CN=InstaWords-Test`) makes the build warn that the Store would reject the package. |
| `Strings/en-US/resources.resw`, `Strings/ar/resources.resw` | `AppDisplayName` (**InstaWords** in both languages: it must be a reserved name) and `AppDescription` (localized). |
| `Assets/*.png` | 35 logos: `Square44x44Logo` at scale-100/125/150/200/400 and targetsize-16/24/32/48/256 (plain, `altform-unplated`, `altform-lightunplated`), `Square150x150Logo`, `Wide310x150Logo` and `StoreLogo` at scale-100/125/150/200/400. **They are committed.** |
| `make-assets.py` | Regenerates `Assets/` from the icon (`site/icon.svg`, redrawn with Pillow so the output is the same everywhere). Run `python3 windows/packaging/msix/make-assets.py` only when the icon changes, then commit. The assets are committed rather than generated in CI because then CI needs no Python or Pillow, and any change to them shows up in review. |
| `build-msix.ps1` | Builds the package. It runs (1) `scripts/msix-stage.mjs`, which copies `InstaWords.exe`, `.exe.config`, `InstaWords.Core.dll`, `Licenses\`, the assets and strings, and writes the manifest with the `package.json` version as **x.y.z.0**; (2) `makepri` → `resources.pri`; (3) `makeappx pack` → the `.msix`. MakeAppx validates the manifest against the schema while it packs. |
| `test-msix.ps1` | CI-only install test (see below). Do not run it on your own PC, because it trusts a new certificate. |

To build it on a Windows PC with the SDK and Node 22.18 or later:

```powershell
dotnet build windows/InstaWords.App -c Release
pwsh windows/packaging/msix/build-msix.ps1        # → out\msix\InstaWords_2.1.0.0_x64.msix
```

### What CI produces and checks

The `store-package` job in `.github/workflows/windows.yml` runs on every push, beside the existing build/installer job, and changes nothing in the `build`, `screenshots` or `release` jobs:

1. Builds the app (Release, `-p:Version=<package.json>`).
2. Runs `build-msix.ps1`, which includes MakeAppx's manifest validation.
3. Uploads the **unsigned** `.msix` as artifact **`store-package`**. This is the file you upload to Partner Center.
4. **Install test** (`test-msix.ps1`) on a **copy** of the package:
   - Signs the copy with a throwaway self-signed certificate whose subject is exactly the Publisher, then trusts it on the runner.
   - Runs `Add-AppxPackage`, then checks the package family name is `FastKiwi.InstaWords_sm1xjmj6v76yc`.
   - Launches the app through `shell:AppsFolder\…!App`, as the Start menu does, and checks it runs from `WindowsApps` and shows its window.
   - Checks Store mode through UI Automation:
     - Settings shows "Updates come from Microsoft Store", with no check button and no auto-update box.
     - "Start with Windows" reads the startup task, switches it on and back off.
     - A changed setting is saved under `%LOCALAPPDATA%\Packages\<family>\LocalCache\Roaming\FastKiwi\InstaWords`.
     - About shows the Store text and has no check button.
   - Uninstalls the app.
5. **Windows App Certification Kit**, if the runner's SDK has it. This step is informational (`continue-on-error`). Its report and the test screenshots go to the artifact `store-package-checks`.

   Latest result: **overall PASS**. 23 tests passed. One optional test, "Blocked executables", is flagged because `InstaWords.exe` references `Process.Start`. The app uses it to open https links in the default browser, to open the data folder in Explorer, and, only outside the Store, to run the verified installer. The certification notes explain this.

The uploaded `store-package` is never signed: the Store signs it.

## 2. Store mode in the app

The app detects the package with kernel32 `GetCurrentPackageFullName`: `APPMODEL_ERROR_NO_PACKAGE` (15700) means not packaged. The code is in `Services/AppPackage.cs`, and the pure logic in `Services/StoreLogic.cs`, which `InstaWords.App.Tests/StoreLogicTests.cs` tests. Installed and portable copies behave exactly as before.

| Area | Store copy |
|---|---|
| **Updater** (`UpdateService`) | Never checks GitHub, never downloads, never runs an installer. `CheckOnStartup`, `CheckAsync`, `DownloadInstallerAsync` and `RunInstallerAndExit` all return early. |
| **Settings, About, tray** | Show «التحديثات تأتي من Microsoft Store» / "Updates come from Microsoft Store" in place of "Check for updates". The auto-update box is hidden. About shows "This Microsoft Store copy does not connect to the internet…" in place of the GitHub note. The tray item shows that text, disabled. |
| **Start with Windows** | The Run key does not work from a package, because its registry writes stay inside the package. So the manifest declares `windows.startupTask` (`TaskId="InstaWordsStartup"`, `Enabled="false"`), and the check box drives it through the WinRT `StartupTask` API (`Microsoft.Windows.SDK.Contracts`, which provides reference metadata only: nothing is added to `bin/`). If the user turned it off in Task Manager → Startup apps, the box is greyed with a note saying to turn it back on there. A policy-controlled state is shown the same way. A launch by the startup task is detected with `AppInstance.GetActivatedEventArgs()` and starts in the tray, like `--tray`. This was chosen over hiding the option because it is the supported mechanism and it keeps the feature; any WinRT error falls back to showing the window, or to a disabled box. |
| **Data folder** | The app still reads and writes `%APPDATA%\FastKiwi\InstaWords`, but MSIX stores the new files in `%LOCALAPPDATA%\Packages\FastKiwi.InstaWords_sm1xjmj6v76yc\LocalCache\Roaming\FastKiwi\InstaWords`. Explorer runs outside the package, so Settings shows that real path and "Open data folder" opens it. The settings hint says the files are removed when the app is uninstalled. |
| **Problem reports** | Technical info says `InstaWords 2.1.0 (Microsoft Store)` and adds the package full name (`MSIX: FastKiwi.InstaWords_2.1.0.0_x64__sm1xjmj6v76yc`). |
| **Global hotkey** (Ctrl+Alt+T, `RegisterHotKey`) | Unchanged. A full-trust app is not in an AppContainer, so `RegisterHotKey` works as usual. |
| **Single instance** | Unchanged: a named mutex plus a named event (`Local\FastKiwi.InstaWords.*`; there is no pipe). Full trust uses the session namespace, so an installed copy and a Store copy also count as one instance. |
| **Printing** | Unchanged: the WPF `PrintDialog` and the "Microsoft Print to PDF" printer work in full trust. |
| **Installer language** (`UiLanguage.InstallerChoice`) | Reads HKCU only on the first run. There is no installer wizard in the Store, so the value is usually missing and the app falls back to "Automatic" (the Windows display language). It already catches any error. |

## 3. Listing text

Partner Center → InstaWords → Store listings → add **English (United States)** and **Arabic**. The package declares `en-US` and `ar`, and each language in the package needs a listing.

### English

**Product name:** InstaWords

**Short description**

> Write any amount in words for cheques, invoices and vouchers, in Arabic, English and French. 100+ currencies, cheque printing, and a Ctrl+Alt+T shortcut that converts the number you copied, from any program. Works fully offline.

**Description**

> InstaWords writes amounts in words (tafqit) for cheques, invoices, receipts and payment vouchers, in Arabic, English and French, with more than 100 currencies and units.
>
> Type a number and the words appear as you type. Arabic grammar is handled for you: dual forms, the right agreement for each currency, and fractions such as halalas and fils. Choose مائة or مئة, add «فقط … لا غير» or "… Only", and copy the result with one click.
>
> Working in Excel or an accounting system? Copy a number, press Ctrl+Alt+T in any program, and the clipboard now holds the amount in words, ready to paste. To convert a whole column, paste it into the Bulk tab or import a CSV file, then copy the results or export them back to CSV.
>
> Print cheques and vouchers: a cheque layout with the amount in figures and in words with a dashed filler, receipt and payment vouchers on A5 or A4 with your company name and logo, and a words-only strip. A live preview shows exactly what prints, and per-field millimetre offsets line it up with your pre-printed cheques. Save as PDF with "Microsoft Print to PDF".
>
> The interface is in Arabic (right to left) or English and switches instantly. A native Windows app: fast, light and fully offline. No account, no ads, and no number ever leaves your computer. Free and open source (MIT).

**Product features** (up to 20)

1. Amounts in words in Arabic, English and French
2. 100+ currencies and units, with their fractions (halalas, fils, cents…)
3. Live conversion as you type, with Arabic-Indic (١٢٣) or Western digits
4. Correct Arabic grammar: dual forms, agreement, مائة or مئة
5. Ready phrases: «فقط … لا غير», "… Only", "Arrêtée la présente facture à la somme de …"
6. Ctrl+Alt+T from any program: the copied number becomes words in the clipboard
7. Bulk tab: paste a column from Excel or import CSV, export to CSV
8. Smooth with 10,000 rows
9. Cheque printing with the amount in figures and in words
10. Receipt and payment vouchers on A5 or A4, with your company name and logo
11. Live print preview, per-field calibration in millimetres, saved cheque profiles
12. Save as PDF with Microsoft Print to PDF
13. Arabic or English interface, switched instantly
14. History of recent conversions
15. Lives in the notification area; optional start with Windows
16. Works fully offline: no account, no ads, nothing sent anywhere
17. Free and open source (MIT)

**Search terms** (up to 7)

`tafqit` · `amount in words` · `cheque writer` · `number to words` · `تفقيط` · `المبلغ بالحروف` · `invoice`

**What's new in this version**

> First Microsoft Store release of InstaWords 2.1: cheque and voucher printing with calibration, an English interface, Bulk conversion of whole Excel columns, and the Ctrl+Alt+T clipboard shortcut. The Store keeps the app up to date.

**Copyright and trademark info:** © 2026 FastKiwi. MIT License.

**Additional license terms:** https://github.com/sarmadiq9090-coder/tafket/blob/HEAD/LICENSE

**Website:** https://fastkiwi.app/instawords/

**Support contact info:** https://github.com/sarmadiq9090-coder/tafket/issues. In the app, "Report a problem or suggestion" opens a prefilled GitHub issue (Settings or the tray menu).

**Privacy policy URL:** https://fastkiwi.app/privacy/

### العربية

<div dir="rtl">

**اسم المنتج:** InstaWords

**وصف مختصر**

> اكتب أي مبلغ بالحروف للشيكات والفواتير والسندات، بالعربية والإنجليزية والفرنسية. أكثر من 100 عملة، وطباعة الشيكات، واختصار Ctrl+Alt+T يحوّل الرقم المنسوخ من أي برنامج. يعمل كله دون إنترنت.

**الوصف**

> InstaWords يكتب المبالغ بالحروف (التفقيط) للشيكات والفواتير وسندات القبض والصرف، بالعربية والإنجليزية والفرنسية، بأكثر من 100 عملة ووحدة.
>
> اكتب الرقم فيظهر المبلغ بالحروف فوراً أثناء الكتابة، بقواعد عربية صحيحة: المثنى، والتمييز المناسب لكل عملة، وأجزاء العملة (هللة، فلس، سنت…). اختر «مائة» أو «مئة»، وأضف «فقط … لا غير»، وانسخ النتيجة بنقرة.
>
> تعمل على Excel أو برنامج محاسبة؟ انسخ الرقم، واضغط Ctrl+Alt+T من أي برنامج، فيصبح ما في الحافظة المبلغَ مكتوباً بالحروف جاهزاً للصق. ولتحويل عمود كامل، الصقه في تبويب «دفعة» أو استورد ملف CSV، ثم انسخ النتائج أو صدّرها CSV.
>
> طباعة الشيكات والسندات: شيك بالمبلغ بالأرقام وبالحروف مع الخط المتقطع بعده، وسند قبض وسند صرف على A5 أو A4 باسم مؤسستك وشعارها، وشريط للمبلغ بالحروف وحده. المعاينة الحيّة هي نفسها ما يُطبع، ومع المعايرة بالمليمتر لكل حقل يطابق الناتج شيكاتك المطبوعة مسبقاً. ولحفظ PDF اختر «Microsoft Print to PDF».
>
> الواجهة بالعربية (من اليمين إلى اليسار) أو بالإنجليزية، وتتبدّل فوراً. برنامج Windows أصلي، سريع وخفيف، يعمل كله دون إنترنت. لا حساب ولا إعلانات، ولا يغادر أي رقم جهازك. مجاني ومفتوح المصدر (MIT).

**المزايا**

1. المبلغ بالحروف بالعربية والإنجليزية والفرنسية
2. أكثر من 100 عملة ووحدة مع أجزائها (هللة، فلس، سنت…)
3. تفقيط فوري أثناء الكتابة، بالأرقام العربية (١٢٣) أو الإنجليزية
4. قواعد عربية صحيحة: المثنى والتمييز، و«مائة» أو «مئة»
5. صيغ جاهزة: «فقط … لا غير»، و"… Only"، والصيغة الفرنسية للفواتير
6. Ctrl+Alt+T من أي برنامج: الرقم المنسوخ يصبح كلمات في الحافظة
7. تبويب «دفعة»: الصق عموداً من Excel أو استورد CSV، وصدّر النتائج CSV
8. سلس مع 10,000 سطر
9. طباعة الشيكات بالمبلغ بالأرقام وبالحروف
10. سند قبض وسند صرف على A5 أو A4 باسم مؤسستك وشعارها
11. معاينة حيّة للطباعة، ومعايرة بالمليمتر لكل حقل، وملفات شيكات محفوظة
12. حفظ PDF عبر Microsoft Print to PDF
13. واجهة بالعربية أو الإنجليزية تتبدّل فوراً
14. سجل لآخر التحويلات
15. يبقى بجانب الساعة في شريط المهام، مع تشغيل اختياري مع Windows
16. يعمل دون إنترنت: لا حساب ولا إعلانات ولا يُرسل شيئاً
17. مجاني ومفتوح المصدر (MIT)

**كلمات البحث**

`تفقيط` · `المبلغ بالحروف` · `كتابة المبالغ` · `شيكات` · `tafqit` · `amount in words` · `cheque writer`

**ما الجديد في هذا الإصدار**

> أول إصدار من InstaWords 2.1 في Microsoft Store: طباعة الشيكات والسندات مع المعايرة، وواجهة إنجليزية، وتحويل أعمدة Excel كاملة في «دفعة»، واختصار Ctrl+Alt+T للحافظة. يحدّث المتجر البرنامج تلقائياً.

**حقوق النشر:** ‏© 2026 FastKiwi. رخصة MIT.

**الدعم:** https://github.com/sarmadiq9090-coder/tafket/issues (أو «الإبلاغ عن مشكلة أو اقتراح» داخل البرنامج).

**سياسة الخصوصية:** https://fastkiwi.app/privacy/

</div>

## 4. Properties, age rating, pricing

- **Category:** Business → *Accounting & finance*. Secondary category, if offered: Productivity.
- **Privacy policy:** https://fastkiwi.app/privacy/. It is being built on the site repository's `claude/privacy` branch, so make sure the page is live before you submit.
- **System requirements:** none special. It runs on Windows 10 version 2004 or later and Windows 11, x64. It also runs on ARM64 Windows 11 under x64 emulation.
- **Product declarations:** leave "accessibility guidelines" unticked unless it has been tested on real hardware with a screen reader. Tick "customers can install to alternate drives" (it can). Untick "uses Windows Mixed Reality".
- **Age rating (IARC questionnaire):** category **Productivity / utility app** (not a game).
  - Violence, fear, sexuality, gambling, language, drugs, alcohol, tobacco: **No**.
  - User-generated content, or users interacting or exchanging content with each other: **No**.
  - Shares the user's location with others: **No**.
  - Shares personal information with third parties: **No**. The Store copy makes no network connections.
  - Digital purchases: **No**.
  - Unrestricted web access (an in-app browser): **No**. Links open in the user's own browser only when clicked.

  The expected rating is **3+ / Everyone**.
- **Pricing and availability:** **Free**, all markets, no free trial, no in-app products. Visibility: public (or "private audience" for a first test).

## 5. Notes for certification

Paste into Submission → **Notes for certification**:

> InstaWords is a classic WPF desktop application (.NET Framework 4.8, built into Windows 10 version 2004 and later and Windows 11) packaged as MSIX. It declares the restricted capability **runFullTrust** because it is a Win32/WPF desktop app (EntryPoint Windows.FullTrustApplication); it does not use any other restricted capability.
>
> - **Global hotkey:** the app registers Ctrl+Alt+T with RegisterHotKey (user-changeable or can be turned off in Settings). Pressing it converts a number on the clipboard to words and puts the words back on the clipboard. It only reads/writes the clipboard when the user presses the hotkey or a button.
> - **Notification area:** the app keeps a tray icon; closing the window keeps it running in the tray (a Settings option). "Exit" in the tray menu quits.
> - **Start with Windows:** off by default; uses the package's startup task (windows.startupTask) when the user enables it in Settings.
> - **Printing:** cheques and vouchers are printed with the standard Windows print dialog (no driver or service is installed).
> - **No account, no sign-in, no ads, no telemetry, no in-app purchases.**
> - **Network:** none. The Store build makes no network connections of its own: the GitHub update check of the non-Store build is disabled in the package (the Store updates the app). The only internet use is user-initiated links (website, source code, license, "Report a problem", which builds a prefilled GitHub issue URL) that open in the user's default browser.
> - **Process.Start** (flagged by the optional WACK "Blocked executables" test): used only to open https links in the user's default browser and the data folder in File Explorer, on the user's click. The non-Store build also uses it to run its downloaded installer; that code path is disabled in the package.
> - **Data:** settings and history are two local JSON files under the user's AppData (redirected by MSIX to the package's LocalCache); nothing is sent anywhere.
> - **Testing:** no test account is needed. Type a number on the first screen; press Ctrl+P for the print preview; Ctrl+2 for bulk conversion.

**Restricted capability justification** (Partner Center asks for it with runFullTrust):

> InstaWords is a desktop (WPF, .NET Framework 4.8) application converted to MSIX; runFullTrust is required for every packaged Win32 desktop app. The app uses it for its normal desktop behaviour: a global hotkey (RegisterHotKey), a notification-area icon, clipboard access on the user's command, and printing via the Windows print dialog.

**What touches the network in the Store build?** I checked every `HttpClient` use, `Process.Start` call and URL in the app:

- `UpdateService` is the only code that makes requests (api.github.com and the GitHub release hosts), and it returns before any request when packaged.
- `Links.Open` (About links, "Report a problem" → a GitHub issue URL) hands an https URL to the default browser, only when the user clicks.
- Fonts and icons are embedded. There is no telemetry, analytics, crash upload or WebView.

## 6. Screenshots

Partner Center needs at least one desktop screenshot of **1366×768 or larger** (PNG), and up to 10 per language.

**On the `ci-screenshots` branch today** (from the UI tests, at the last default-branch run):

| File | Size | Interface | Use for the Store? |
|---|---|---|---|
| `screenshots/01-main-ar.png` | 1040×800 | Arabic | Too small |
| `screenshots/02-main-en.png` | 1040×800 | Arabic UI, English output | Too small |
| `screenshots/03-main-error.png`, `04-options.png`, `05-bulk.png`, `08-settings.png` | 1040×800 | Arabic | Too small |
| `screenshots/09-about.png` (560×789), `10-report.png` (760×720) | small | Arabic | No |
| `screenshots/11-update.png` | 600×464 | Arabic | **No**: shows the GitHub updater, which the Store copy does not have |
| `screenshots/16-print-cheque.png` … `21-print-bulk.png` | 1180×820 | Arabic | Too narrow |
| `screenshots/12-main-en-ui.png`, `13-bulk-en-ui.png`, `14-settings-en-ui.png`, `22-language-switch-en.png` | 1040×800 | English | Too small |
| `screenshots/15-print-en-ui.png` | 1180×820 | English | Too narrow |
| `screenshots/06-toast.png`, `07-toast-error.png` | 408×129 | Arabic | No |

None of these meets 1366×768. Arabic and English 1366×768 screenshots were both missing, so this branch adds `windows/InstaWords.UITests/StoreScreenshotTests.cs`. It runs in the normal UI tests on a 1920×1080 runner screen and writes:

| File | Size | Content |
|---|---|---|
| `store-ar-1-convert.png` / `store-en-1-convert.png` | 1366×768 | Main window, 1,250.75 in words, formatting options open (English listing: English UI and English words) |
| `store-ar-2-bulk.png` / `store-en-2-bulk.png` | 1366×768 | Bulk tab with six converted rows |
| `store-ar-3-settings.png` / `store-en-3-settings.png` | 1366×768 | Settings (top of the page: interface language, defaults, hotkey) |
| `store-ar-4-print.png` / `store-en-4-print.png` | 1440×900 | Receipt voucher print preview over the main window |

Until this branch is merged, get them from the workflow run's `InstaWords-<version>-screenshots` artifact. After the merge they also appear on `ci-screenshots`. The UI tests run the unpackaged build, so if you want the Store-mode screens (no update button), the install test's screenshots are in the `store-package-checks` artifact (`store-settings.png`, `store-about.png`, at the default window size).

## 7. Submission checklist (owner)

1. ✅ **Reserve the name** "InstaWords" (done: Store ID 9P62P4WQSQNK).
2. ✅ **Copy the identity**: Partner Center → Apps and games → InstaWords → **Product management → Product identity** shows `Package/Identity/Name`, `Package/Identity/Publisher` and `Package/Properties/PublisherDisplayName`. They are already in `windows/packaging/msix/identity.json` (done on this branch). If you ever change the publisher display name, update that file too.
3. **Publish the privacy policy** at https://fastkiwi.app/privacy/ (site repository, branch `claude/privacy`).
4. **Build:** push to the branch you release from, or run Actions → "Windows (native app)" → Run workflow. Wait for the `store-package` job to pass. The install test must be green.
5. **Download** the `store-package` artifact from that run (a zip containing `InstaWords_<version>.0_x64.msix`) and unzip it. Do **not** sign it.
6. Partner Center → InstaWords → **Start your submission**:
   1. **Pricing and availability:** Free, markets, visibility (section 4).
   2. **Properties:** category Business → Accounting & finance, privacy policy URL, support info, product declarations.
   3. **Age ratings:** the questionnaire answers in section 4.
   4. **Packages:** drag in the `.msix`. Partner Center validates it, and the version must be higher than any earlier submission.
   5. **Store listings:** English (United States) and Arabic. Paste section 3, then add at least one 1366×768 screenshot per language (section 6). The Store takes the app's logos from the package.
   6. **Submission options → Notes for certification:** section 5, including the runFullTrust justification.
7. **Submit.** Certification usually takes 1 to 3 business days. Partner Center emails you the result.
8. **Later versions:** bump `package.json` (`npm run release:prepare -- x.y.z`), let CI build `store-package`, then make a new submission with the new `.msix` (each version must be higher). The Store delivers the update. The app never self-updates in the Store copy.

## 8. PWA alternative

The web app at https://fastkiwi.app/instawords/ is already a PWA: it has `site/manifest.webmanifest` and an offline service worker, `site/sw.js`. **PWABuilder** (pwabuilder.com) can wrap it into a Store package too.

- **Is it faster?** Only slightly. The Partner Center steps are the same: name reservation, listing, age rating and privacy policy. PWABuilder also needs work the site does not have yet:
  - PNG icons of 512×512 and smaller; the manifest has only an SVG.
  - Screenshots in the manifest.
  - A PWABuilder-generated package tied to the same Partner Center identity. One Store product holds one app type, so a PWA would need its own name reservation or would replace the native package.
- **What would users get?** Less. There is no global Ctrl+Alt+T hotkey, no tray icon and no start with Windows. Cheque calibration and printing go through the browser's print dialog.

**Recommendation:** ship the native MSIX as the primary Store listing (this branch). It is ready, it is the product's main strength, and CI builds and install-tests it on every push. Consider a PWABuilder listing only later, for example under a separate name such as "InstaWords Web", if a lighter web version is wanted for Store discovery.

## 9. Not verified yet

- **Not installed on a real PC.** The package has only been installed on GitHub's Windows Server runner, with a self-signed copy, so there are no Store-signed or Windows 10/11 desktop runs yet. On a real PC, check:
  - start with Windows after a sign-in, which should start in the tray;
  - Task Manager → Startup apps lists InstaWords;
  - Ctrl+Alt+T from Excel;
  - printing to a real printer;
  - "Open data folder";
  - uninstall removes the data.
- **Partner Center validation of this exact package** happens only when you upload it. MakeAppx schema validation and the (informational) WACK run in CI are the closest checks available. WACK ran on the Windows Server runner "without application deployment", so its runtime tests (launch and suspend times, crashes) were not part of it.
- **Installed copy and Store copy side by side:** both share one single-instance lock. The Store copy reads an existing `%APPDATA%\FastKiwi\InstaWords` (MSIX shows the real folder merged with its own). New files go to its LocalCache. This has not been tested on a PC that has both.
- **The privacy policy page** is not live yet (site branch `claude/privacy`).

