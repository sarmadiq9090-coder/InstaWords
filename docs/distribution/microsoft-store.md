# InstaWords in the Microsoft Store · النشر في متجر Microsoft

<div dir="rtl">

**الخلاصة بالعربية:** برنامج InstaWords الأصلي (WPF) يُعبّأ حزمة **MSIX** يبنيها GitHub Actions مع كل إيداع، باسم الملف المؤقت `store-package`. الحزمة غير موقّعة لأن المتجر يوقّعها بنفسه، وهوية Partner Center الحقيقية موجودة في `windows/packaging/msix/identity.json`. في نسخة المتجر لا يتحقق البرنامج من التحديثات ولا ينزّل شيئاً، ويعرض بدلاً من ذلك «التحديثات تأتي من Microsoft Store». ويعمل «التشغيل مع Windows» عبر مهمة بدء التشغيل الخاصة بالحزمة. إصدار نسخة المتجر الحالي **2.2.1** (الحزمة 2.2.1.0) من الحقل `storeVersion` في `identity.json`، ويجب رفعه قبل كل إرسال جديد (القسم 1). نصوص صفحة المتجر بالعربية والإنجليزية، وقائمة خطوات الإرسال، في الأقسام أدناه.

**دليل المالك خطوة بخطوة بالعربية:** [`microsoft-store-owner-ar.md`](microsoft-store-owner-ar.md) (روابط التنزيل المباشرة، وكل ما يُلصق في Partner Center).

</div>

| | |
|---|---|
| Store ID | **9P62P4WQSQNK** |
| Store page (after it is published) | https://apps.microsoft.com/detail/9P62P4WQSQNK · `ms-windows-store://pdp/?ProductId=9P62P4WQSQNK` |
| Package/Identity/Name | `FastKiwi.InstaWords` |
| Package/Identity/Publisher | `CN=8EFD6949-D088-481F-886D-82300679C5E5` |
| Package/Properties/PublisherDisplayName | `FastKiwi` |
| Package family name | `FastKiwi.InstaWords_sm1xjmj6v76yc` (CI checks the installed test copy against it) |
| Store version (this submission) | **2.2.1**, package version **2.2.1.0** (`storeVersion` in `identity.json`; `package.json` stays 2.2.0) |
| Privacy policy | https://fastkiwi.app/privacy/ (live; it has an InstaWords section that covers the Store copy) |
| Website | https://fastkiwi.app/instawords/ |
| Support email | support@fastkiwi.app |
| Package file | `InstaWords_2.2.1.0_x64.msix`, workflow artifact **`store-package`**: [run 42218450222 → store-package](https://github.com/sarmadiq9090-coder/InstaWords/actions/runs/42218450222/artifacts/13502541588) |
| Store screenshots | [run 42218450222 → InstaWords-2.2.0-screenshots](https://github.com/sarmadiq9090-coder/InstaWords/actions/runs/42218450222/artifacts/13501703662) (the eight `store-*.png`) |
| Owner guide (Arabic) | [`microsoft-store-owner-ar.md`](microsoft-store-owner-ar.md) |

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
| `identity.json` | The Partner Center identity (table above). `"placeholder": true` (or the test publisher `CN=InstaWords-Test`) makes the build warn that the Store would reject the package. Optional `"storeVersion"`: the version of the Store build when it runs ahead of `package.json` (see [Version of each submission](#version-of-each-submission)). |
| `Strings/en-US/resources.resw`, `Strings/ar/resources.resw` | `AppDisplayName` (**InstaWords** in both languages: it must be a reserved name) and `AppDescription` (localized). |
| `Assets/*.png` | 35 logos: `Square44x44Logo` at scale-100/125/150/200/400 and targetsize-16/24/32/48/256 (plain, `altform-unplated`, `altform-lightunplated`), `Square150x150Logo`, `Wide310x150Logo` and `StoreLogo` at scale-100/125/150/200/400. **They are committed.** |
| `make-assets.py` | Regenerates `Assets/` from the icon, `windows/branding/instawords.svg` (the pen nib writing a stroke; the same file as `site/icon.svg`), rasterized with Pillow alone by `windows/branding/brandicon.py`, so the output is the same everywhere. The green tile for Start, the app list and the Store; the nib and stroke alone for the taskbar (`altform-unplated`: white and lime for a dark taskbar; `altform-lightunplated`: teal and green for a light one). Run `python3 windows/packaging/msix/make-assets.py` only when the icon changes, then commit. `windows/branding/make-icons.py` makes the app's `.ico` and in-app logo from the same file. The assets are committed rather than generated in CI because then CI needs no Python or Pillow, and any change to them shows up in review. |
| `build-msix.ps1` | Builds the package. It runs (1) `scripts/msix-stage.mjs`, which copies `InstaWords.exe`, `.exe.config`, `InstaWords.Core.dll`, `Licenses\`, the assets and strings, and writes the manifest with the Store version as **x.y.z.0** (`storeVersion`, else `package.json`); (2) `makepri` → `resources.pri`; (3) `makeappx pack` → the `.msix`. MakeAppx validates the manifest against the schema while it packs. |
| `test-msix.ps1` | CI-only install test (see below). Do not run it on your own PC, because it trusts a new certificate. |

To build it on a Windows PC with the SDK and Node 22.18 or later:

```powershell
dotnet build windows/InstaWords.App -c Release
$v = node scripts/msix-version.mjs                 # 2.2.1 (storeVersion, else package.json)
dotnet build windows/InstaWords.App -c Release -p:Version=$v
pwsh windows/packaging/msix/build-msix.ps1 -Version $v   # → out\msix\InstaWords_2.2.1.0_x64.msix
```

### Version of each submission

Partner Center accepts a package only if its version is **higher** than every package it already has for InstaWords, and the fourth number must stay 0. The Store version is:

- `storeVersion` in `windows/packaging/msix/identity.json`, when it is set (now **2.2.1**: the first Store submission, a Store build on top of the 2.2.0 code with the Store fixes, while `package.json` and the GitHub release stay 2.2.0);
- otherwise the `package.json` version.

`scripts/msix-version.mjs` prints it, and the `store-package` job uses it twice: `dotnet build -p:Version=<it>` (so About in the Store copy shows 2.2.1) and the MSIX `Version` `<it>.0`. It refuses a `storeVersion` that is not plain `x.y.z` or is **lower** than `package.json` (`npm test` fails too, in `tests/msix.test.ts`), so the Store copy can never look older than the GitHub one.

**Before each new Store submission:**

1. Pick the next version, higher than the one last uploaded to Partner Center (the last one is in Partner Center → InstaWords → Packages; this branch's is 2.2.1.0).
2. If `package.json` already has a higher version (a normal release, `npm run release:prepare -- x.y.z`), delete `storeVersion` from `identity.json`: the Store build then follows `package.json`. Otherwise set `"storeVersion": "x.y.z"` (for example 2.2.2 for another Store-only fix).
3. Update the version in the `identity.json storeVersion` test of `tests/msix.test.ts` (it pins the value on purpose, so a change is always deliberate), run `npm test`, push, and upload the new `store-package`.

### What CI produces and checks

The `store-package` job in `.github/workflows/windows.yml` runs on every push, beside the existing build/installer job, and changes nothing in the `build`, `screenshots` or `release` jobs:

1. Reads the Store version (`node scripts/msix-version.mjs`, 2.2.1 now) and builds the app with it (Release, `-p:Version=2.2.1`).
2. Runs `build-msix.ps1`, which includes MakeAppx's manifest validation.
3. Uploads the **unsigned** `.msix` as artifact **`store-package`**. This is the file you upload to Partner Center.
4. **Install test** (`test-msix.ps1`) on a **copy** of the package:
   - Signs the copy with a throwaway self-signed certificate whose subject is exactly the Publisher, then trusts it on the runner.
   - Runs `Add-AppxPackage`, then checks the package family name is `FastKiwi.InstaWords_sm1xjmj6v76yc` and the package version is the Store version `.0` (2.2.1.0).
   - Launches the app through `shell:AppsFolder\…!App`, as the Start menu does, and checks it runs from `WindowsApps` and shows its window.
   - Checks Store mode through UI Automation:
     - Settings shows "Updates come from Microsoft Store", with no check button and no auto-update box.
     - "Start with Windows" reads the startup task, switches it on and back off.
     - A changed setting is saved under `%LOCALAPPDATA%\Packages\<family>\LocalCache\Roaming\FastKiwi\InstaWords`.
     - About shows the Store text, has no check button, and shows the Store version (2.2.1).
   - Uninstalls the app.
5. **Windows App Certification Kit**, if the runner's SDK has it. This step is informational (`continue-on-error`). Its report and the test screenshots go to the artifact `store-package-checks`.

   Latest result (run [42218450222](https://github.com/sarmadiq9090-coder/InstaWords/actions/runs/42218450222), package 2.2.1.0): **overall PASS**. 23 tests passed. One optional test, "Blocked executables", is flagged because `InstaWords.exe` references `Process.Start`. The app uses it to open https links in the default browser, to open the data folder in Explorer, and, only outside the Store, to run the verified installer. The certification notes explain this.

The uploaded `store-package` is never signed: the Store signs it.

## 2. Store mode in the app

The app detects the package with kernel32 `GetCurrentPackageFullName`: `APPMODEL_ERROR_NO_PACKAGE` (15700) means not packaged. The code is in `Services/AppPackage.cs`, and the pure logic in `Services/StoreLogic.cs`, which `InstaWords.App.Tests/StoreLogicTests.cs` tests. Installed and portable copies behave exactly as before.

| Area | Store copy |
|---|---|
| **Updater** (`UpdateService`) | Never checks GitHub, never downloads, never runs an installer. `CheckOnStartup`, `CheckAsync`, `DownloadInstallerAsync` and `RunInstallerAndExit` all return early. |
| **Settings, About, tray** | Show «التحديثات تأتي من Microsoft Store» / "Updates come from Microsoft Store" in place of "Check for updates". The auto-update box is hidden. About shows "This Microsoft Store copy does not connect to the internet…" in place of the GitHub note. The tray item shows that text, disabled. |
| **Start with Windows** | The Run key does not work from a package, because its registry writes stay inside the package. So the manifest declares `windows.startupTask` (`TaskId="InstaWordsStartup"`, `Enabled="false"`), and the check box drives it through the WinRT `StartupTask` API (`Microsoft.Windows.SDK.Contracts`, which provides reference metadata only: nothing is added to `bin/`). If the user turned it off in Task Manager → Startup apps, the box is greyed with a note saying to turn it back on there. A policy-controlled state is shown the same way. A launch by the startup task is detected with `AppInstance.GetActivatedEventArgs()` and starts in the tray, like `--tray`. This was chosen over hiding the option because it is the supported mechanism and it keeps the feature; any WinRT error falls back to showing the window, or to a disabled box. |
| **Data folder** | The app still reads and writes `%APPDATA%\FastKiwi\InstaWords`, but MSIX stores the new files in `%LOCALAPPDATA%\Packages\FastKiwi.InstaWords_sm1xjmj6v76yc\LocalCache\Roaming\FastKiwi\InstaWords`. Explorer runs outside the package, so Settings shows that real path and "Open data folder" opens it. The settings hint says the files are removed when the app is uninstalled. |
| **Problem reports** | Technical info says `InstaWords 2.2.1 (Microsoft Store)` and adds the package full name (`MSIX: FastKiwi.InstaWords_2.2.1.0_x64__sm1xjmj6v76yc`). |
| **Global hotkey** (Ctrl+Alt+T, `RegisterHotKey`) | Unchanged. A full-trust app is not in an AppContainer, so `RegisterHotKey` works as usual. |
| **Single instance** | Unchanged: a named mutex plus a named event (`Local\FastKiwi.InstaWords.*`; there is no pipe). Full trust uses the session namespace, so an installed copy and a Store copy also count as one instance. |
| **Printing** | Unchanged: the WPF `PrintDialog` and the "Microsoft Print to PDF" printer work in full trust. |
| **Installer language** (`UiLanguage.InstallerChoice`) | Reads HKCU only on the first run. There is no installer wizard in the Store, so the value is usually missing and the app falls back to "Automatic" (the Windows display language). It already catches any error. |

## 3. Listing text

Partner Center → InstaWords → Store listings → add **English (United States)** and **Arabic**. The package declares exactly two languages, `en-US` and `ar` (the manifest's `<Resources>` and the two `Strings/<language>/resources.resw`), and each language in the package needs a listing. If Partner Center lists any other listing language, remove it (Store listings → Manage additional languages): the app's interface is translated only into Arabic and English. The owner guide has every field: [`microsoft-store-owner-ar.md`](microsoft-store-owner-ar.md).

### English

**Product name:** InstaWords

**Short description**

> Turn any amount into correct words for cheques, invoices and vouchers, in Arabic, English or French. More than 50 currencies, cheque printing, whole Excel columns. Offline and free.

**Description**

> InstaWords writes amounts in words for cheques, invoices, receipts and payment vouchers, in Arabic, English and French, with more than 50 currencies and over 50 other units (weight, length, area, time, quantities…). Writing the amount in words, as cheques and vouchers require, is called tafqit (تفقيط) in Arabic.
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
2. More than 50 currencies, with fractions such as halalas, fils and cents, and over 50 units of measure
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
17. Free (proprietary licence, see LICENSE)

**Search terms** (up to 7)

`tafqit` · `amount in words` · `cheque writer` · `number to words` · `تفقيط` · `المبلغ بالحروف` · `invoice`

**What's new in this version**

> First Microsoft Store release (version 2.2.1): amounts in words in Arabic, English and French, cheque and voucher printing with calibration and a print preview that fits the whole page, Bulk conversion of whole Excel columns, and the Ctrl+Alt+T clipboard shortcut. The Store keeps the app up to date.

**Copyright and trademark info:** © 2026 FastKiwi (Sarmad)

**Additional license terms:** https://github.com/sarmadiq9090-coder/InstaWords/blob/HEAD/LICENSE

**Website:** https://fastkiwi.app/instawords/

**Support contact info:** support@fastkiwi.app. (In the app, "Report a problem or suggestion" opens a prefilled GitHub issue: Settings or the tray menu.)

**Privacy policy URL:** https://fastkiwi.app/privacy/

### العربية

<div dir="rtl">

**اسم المنتج:** InstaWords

**وصف مختصر**

> اكتب أي مبلغ بالحروف كتابةً صحيحة للشيكات والفواتير والسندات، بالعربية أو الإنجليزية أو الفرنسية. أكثر من 50 عملة، وطباعة الشيكات، وأعمدة Excel كاملة. يعمل دون إنترنت، ومجاني.

**الوصف**

> InstaWords يكتب المبالغ بالحروف (التفقيط) للشيكات والفواتير وسندات القبض والصرف، بالعربية والإنجليزية والفرنسية، بأكثر من 50 عملة وأكثر من 50 وحدة أخرى (الوزن والطول والمساحة والوقت والكميات…).
>
> اكتب الرقم فيظهر المبلغ بالحروف فوراً أثناء الكتابة، بقواعد عربية صحيحة: المثنى، والتمييز المناسب لكل عملة، وأجزاء العملة (هللة، فلس، سنت…). اختر «مائة» أو «مئة»، وأضف «فقط … لا غير»، وانسخ النتيجة بنقرة.
>
> تعمل على Excel أو برنامج محاسبة؟ انسخ الرقم، واضغط Ctrl+Alt+T من أي برنامج، فيصبح ما نسخته (الحافظة) المبلغَ مكتوباً بالحروف جاهزاً للصق. ولتحويل عمود كامل، الصقه في تبويب «عدة مبالغ» أو استورد ملف CSV، ثم انسخ النتائج أو صدّرها CSV.
>
> طباعة الشيكات والسندات: شيك بالمبلغ بالأرقام وبالحروف مع الخط المتقطع بعده، وسند قبض وسند صرف على A5 أو A4 باسم مؤسستك وشعارها، وشريط للمبلغ بالحروف وحده. المعاينة الحيّة هي نفسها ما يُطبع، ومع المعايرة بالمليمتر لكل حقل يطابق الناتج شيكاتك المطبوعة مسبقاً. ولحفظ PDF اختر «Microsoft Print to PDF».
>
> الواجهة بالعربية (من اليمين إلى اليسار) أو بالإنجليزية، وتتبدّل فوراً. برنامج Windows أصلي، سريع وخفيف، يعمل كله دون إنترنت. لا حساب ولا إعلانات، ولا يغادر أي رقم جهازك. مجاني ومفتوح المصدر (MIT).

**المزايا**

1. المبلغ بالحروف بالعربية والإنجليزية والفرنسية
2. أكثر من 50 عملة مع أجزائها مثل الهللة والفلس والسنت، وأكثر من 50 وحدة قياس
3. تفقيط فوري أثناء الكتابة، بالأرقام العربية (١٢٣) أو الإنجليزية
4. قواعد عربية صحيحة: المثنى والتمييز، و«مائة» أو «مئة»
5. صيغ جاهزة: «فقط … لا غير»، و"… Only"، والصيغة الفرنسية للفواتير
6. Ctrl+Alt+T من أي برنامج: الرقم الذي نسخته يصبح كلمات جاهزة للصق
7. تبويب «عدة مبالغ»: الصق عموداً من Excel أو استورد CSV، وصدّر النتائج CSV
8. سلس مع 10,000 سطر
9. طباعة الشيكات بالمبلغ بالأرقام وبالحروف
10. سند قبض وسند صرف على A5 أو A4 باسم مؤسستك وشعارها
11. معاينة حيّة للطباعة، ومعايرة بالمليمتر لكل حقل، وملفات شيكات محفوظة
12. حفظ PDF عبر Microsoft Print to PDF
13. واجهة بالعربية أو الإنجليزية تتبدّل فوراً
14. سجل لآخر التحويلات
15. يبقى بجانب الساعة في شريط المهام، مع تشغيل اختياري مع Windows
16. يعمل دون إنترنت: لا حساب ولا إعلانات ولا يُرسل شيئاً
17. مجاني (رخصة مملوكة، انظر LICENSE)

**كلمات البحث**

`تفقيط` · `كتابة المبلغ بالحروف` · `تحويل الأرقام إلى كلمات` · `المبلغ بالحروف` · `كتابة المبالغ` · `طباعة شيك` · `شيكات`

**ما الجديد في هذا الإصدار**

> أول إصدار في Microsoft Store (الإصدار 2.2.1): المبلغ بالحروف بالعربية والإنجليزية والفرنسية، وطباعة الشيكات والسندات مع المعايرة ومعاينة تعرض الصفحة كاملة، وتحويل أعمدة Excel كاملة في «عدة مبالغ»، واختصار Ctrl+Alt+T لتحويل ما نسخته. يحدّث المتجر البرنامج تلقائياً.

**حقوق النشر:** ‏© 2026 FastKiwi (Sarmad)

**الدعم:** support@fastkiwi.app (أو «الإبلاغ عن مشكلة أو اقتراح» داخل البرنامج).

**سياسة الخصوصية:** https://fastkiwi.app/privacy/

</div>

## 4. Properties, age rating, pricing

- **Category:** Business → *Accounting & finance*. Secondary category, if offered: Productivity.
- **Privacy policy:** https://fastkiwi.app/privacy/ (live; its InstaWords part says the Store copy makes no network connections of its own).
- **Website:** https://fastkiwi.app/instawords/
- **Support contact info:** support@fastkiwi.app
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
- **Pricing and availability:** **Free**, all markets, no free trial, no in-app products. Visibility: public.
- **Publishing:** Submission options → publish automatically as soon as it passes certification.

## 5. Notes for certification

Partner Center has moved this box: paste it into **Supplemental info → Additional Testing Information** (formerly Submission options → Notes for certification):

> InstaWords is a classic WPF desktop application (.NET Framework 4.8, built into Windows 10 version 2004 and later and Windows 11) packaged as MSIX. It declares the restricted capability **runFullTrust** because it is a Win32/WPF desktop app (EntryPoint Windows.FullTrustApplication); it does not use any other restricted capability.
>
> - **Global hotkey:** the app registers Ctrl+Alt+T with RegisterHotKey (user-changeable or can be turned off in Settings). Pressing it converts a number on the clipboard to words and puts the words back on the clipboard. It only reads/writes the clipboard when the user presses the hotkey or a button.
> - **Notification area:** the app keeps a tray icon; closing the window keeps it running in the tray (a Settings option). "Exit" in the tray menu quits.
> - **Start with Windows:** off by default; uses the package's startup task (windows.startupTask) when the user enables it in Settings.
> - **Printing:** cheques and vouchers are printed with the standard Windows print dialog (no driver or service is installed).
> - **No account, no sign-in, no ads, no telemetry, no in-app purchases.**
> - **Network:** none. The Store build makes no network connections of its own: the GitHub update check of the non-Store build is disabled in the package (the Store updates the app). The only internet use is user-initiated links (website, source code, license, "Report a problem", which builds a prefilled GitHub issue URL) that open in the user's default browser.
> - **Process.Start** (flagged by the optional WACK "Blocked executables" test): used only to open https links in the user's default browser and the data folder in File Explorer, on the user's click. The non-Store build also uses it to run its downloaded installer; that code path is disabled in the package.
> - **Data:** settings and history are two local JSON files, plus a local crash log, under the user's AppData (redirected by MSIX to the package's LocalCache); nothing is sent anywhere.
> - **Testing:** no test account is needed. Type a number on the first screen; press Ctrl+P for the print preview; Ctrl+2 for bulk conversion.

**Restricted capability justification** (Partner Center asks for it with runFullTrust; the box takes at most 500 characters, and this text is 447):

> InstaWords is a classic WPF desktop app (.NET Framework 4.8) packaged as MSIX, so it needs runFullTrust like every packaged Win32 app. It uses it only for normal desktop behaviour: a global hotkey (Ctrl+Alt+T, RegisterHotKey), a notification-area icon, clipboard access when the user asks, printing through the Windows print dialog, and an optional startup task. It needs no other restricted capability and makes no network connections of its own.

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

None of these meets 1366×768, so `windows/InstaWords.UITests/StoreScreenshotTests.cs` takes the Store screenshots. It runs in the normal UI tests on the 1920×1080 runner screen. It maximises the main window (and the print dialog) and captures the screen's work area, which the maximised window fills exactly: no desktop and no taskbar show. Before the first picture it converts and copies four amounts through the app, so the history holds real results, and it scrolls each page to the top. Every amount in words, history entry and preview is the app's own output.

Upload them in this order, the same for both languages (Arabic listing: `store-ar-*`, Arabic interface and Arabic words; English listing: `store-en-*`, English interface and English words):

| # | File | Size | Content |
|---|---|---|---|
| 1 | `store-ar-1-convert.png` / `store-en-1-convert.png` | 1920×1032 | Convert tab: 45,750.25 SAR (English: 18,945.75 USD) in words with «فقط … لا غير» / "… Only", the formatting options open, and five recent conversions in SAR, AED, IQD, USD (English: USD, AED, SAR, EUR) in the history |
| 2 | `store-ar-2-print.png` / `store-en-2-print.png` | 1920×1032 | Print dialog, maximised: a receipt voucher with a company name and logo (the app icon), number 1042, today's date, payer and purpose, and its live preview |
| 3 | `store-ar-3-excel.png` / `store-en-3-excel.png` | 1920×1032 | «عدة مبالغ» / Bulk tab: a column copied from Excel (header row and seven formatted amounts) pasted and converted, in SAR (English: AED) |
| 4 | `store-ar-4-settings.png` / `store-en-4-settings.png` | 1920×1032 | Settings, from the top: interface language, default conversion options, the Ctrl+Alt+T shortcut |

**Captions** (Partner Center allows one per screenshot, up to 200 characters). Paste the English ones in the English listing and the Arabic ones in the Arabic listing, beside the screenshot with the same number:

| # | English | العربية |
|---|---|---|
| 1 | Type an amount and the words appear as you type, ready for the cheque. Recent amounts stay in the history. | <span dir="rtl">اكتب المبلغ فيظهر بالحروف فوراً، جاهزاً للشيك. وآخر المبالغ محفوظة في السجل.</span> |
| 2 | Print receipt and payment vouchers with your company name and logo, or fill in your pre-printed cheques. | <span dir="rtl">اطبع سندات القبض والصرف باسم مؤسستك وشعارها، أو املأ شيكاتك المطبوعة مسبقاً.</span> |
| 3 | Paste a column from Excel and get every amount in words at once. Copy the results back or export them to CSV. | <span dir="rtl">الصق عموداً من Excel فتحصل على كل المبالغ بالحروف في خطوة واحدة. انسخ النتائج أو صدّرها إلى CSV.</span> |
| 4 | Arabic or English interface, your default currency and wording, and Ctrl+Alt+T to convert a copied number in any program. | <span dir="rtl">واجهة بالعربية أو الإنجليزية، وعملتك وصيغتك الافتراضيتان، وCtrl+Alt+T لتحويل الرقم الذي نسخته من أي برنامج.</span> |

Get them from the workflow run's `InstaWords-<version>-screenshots` artifact (the UI tests build the app from `package.json`, so the artifact is named `InstaWords-2.2.0-screenshots`; the screens show no version number). The direct links of the run used for this submission are in [`microsoft-store-owner-ar.md`](microsoft-store-owner-ar.md). After the merge they also appear on `ci-screenshots`. The UI tests run the unpackaged build, so Settings shows the GitHub update options, which the Store copy replaces with "Updates come from Microsoft Store". If you want the Store-mode screens, the install test's screenshots are in the `store-package-checks` artifact (`store-settings.png`, `store-about.png`, at the default window size).

## 7. Submission checklist (owner)

1. ✅ **Reserve the name** "InstaWords" (done: Store ID 9P62P4WQSQNK).
2. ✅ **Copy the identity**: Partner Center → Apps and games → InstaWords → **Product management → Product identity** shows `Package/Identity/Name`, `Package/Identity/Publisher` and `Package/Properties/PublisherDisplayName`. They are already in `windows/packaging/msix/identity.json` (done on this branch). If you ever change the publisher display name, update that file too.
3. ✅ **Privacy policy** is live at https://fastkiwi.app/privacy/.
4. **Build:** push to the branch you release from, or run Actions → "Windows (native app)" → Run workflow. Wait for the `store-package` job to pass. The install test must be green.
5. **Download** the `store-package` artifact from that run (a zip containing `InstaWords_2.2.1.0_x64.msix`) and unzip it. Do **not** sign it.
6. Partner Center → InstaWords → **Start your submission**:
   1. **Pricing and availability:** Free, markets, visibility (section 4).
   2. **Properties:** category Business → Accounting & finance, privacy policy URL, website, support email support@fastkiwi.app, product declarations.
   3. **Age ratings:** the questionnaire answers in section 4.
   4. **Packages:** drag in the `.msix`. Partner Center validates it, and the version must be higher than any earlier submission.
   5. **Store listings:** English (United States) and Arabic only (remove any other language Partner Center adds). Paste section 3, then add the four screenshots per language in order, each with its caption (section 6). The Store takes the app's logos from the package.
   6. **Supplemental info → Additional Testing Information:** section 5; the runFullTrust justification goes in the restricted-capability box.
   7. **Submission options:** publish automatically as soon as it passes certification.
7. **Submit.** Certification usually takes 1 to 3 business days. Partner Center emails you the result.
8. **Later versions:** raise the Store version first ([Version of each submission](#version-of-each-submission)): either bump `package.json` (`npm run release:prepare -- x.y.z`) and delete `storeVersion`, or raise `storeVersion`. Let CI build `store-package`, then make a new submission with the new `.msix` (each version must be higher). The Store delivers the update. The app never self-updates in the Store copy.

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
- **The Excel add-in** (`InstaWords.xlam`, GitHub releases only) is not in the Store package and is not mentioned in the listing.

