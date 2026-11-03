# InstaWords «تفقيط» for Windows

<div dir="rtl">

## بالعربية

**InstaWords** برنامج Windows **أصلي** يكتب المبالغ بالحروف للشيكات والفواتير والسندات، بالعربية والإنجليزية والفرنسية، بأكثر من 100 عملة ووحدة. مبني بـ WPF على ‎.NET Framework 4.8 المدمج في Windows 10 و11، فلا يحتاج تثبيت أي شيء إضافي، ولا يحتوي أي متصفح أو WebView، ويعمل كله دون إنترنت. نتائجه مطابقة حرفاً بحرف للموقع ولدالة Excel لأن محرّكه (`InstaWords.Core`) مولَّد من النواة نفسها.

### المزايا

- **تفقيط فوري أثناء الكتابة**: أرقام عربية (١٢٣) أو إنجليزية، بفواصل الآلاف أو بدونها، ويظهر «الرقم المعتمد» منسّقاً لتتحقق منه.
- **العملة أو الوحدة** مجمّعة حسب الفئة، مع بحث بالعربية أو الرمز (SAR) أو الاسم الإنجليزي والفرنسي: اكتب وأنت على القائمة ثم Enter.
- **لغة التفقيط**: العربية والإنجليزية والفرنسية، والنص يُعرض بالاتجاه الصحيح لكل لغة.
- **خيارات الصياغة**: مائة أو مئة، وتقريب الخانات الزائدة أو إظهار خطأ، ونص قبل المبلغ وبعده مع صيغ جاهزة لكل لغة: «فقط … لا غير»، و"… Only"، و«Arrêtée la présente facture à la somme de …».
- **نسخ بنقرة** (أو Ctrl+Enter)، و**سجل** لآخر التحويلات تعيد استخدامها بنقرة.
- **تبويب «دفعة»**: الصق عموداً من Excel أو استورد CSV/TXT (يتعرّف على سطر العناوين والقيم بين علامات التنصيص)، وانسخ النتائج سطراً لكل رقم، أو صدّرها CSV بترميز UTF-8 يفتحه Excel بالعربية. يتحمّل 10,000 صف دون تجمّد.
- **Ctrl+Alt+T من أي برنامج**: انسخ رقماً، اضغط الاختصار، فيصبح ما في الحافظة المبلغَ مكتوباً بالحروف مع إشعار صغير. يمكن تغيير الاختصار من الإعدادات إن كان برنامج آخر يستخدمه.
- **شريط المهام**: يبقى البرنامج بجانب الساعة عند الإغلاق (قابل للتغيير)، وقائمته: فتح، تحويل الحافظة، الإعدادات، التحقق من وجود تحديث، الإبلاغ عن مشكلة أو اقتراح، خروج. ونسخة واحدة فقط تعمل، و«تشغيل مع Windows» اختياري.
- **التحديث**: يتحقق مرة يومياً على الأكثر من صفحة الإصدارات في GitHub (يمكن إيقافه)، ويُنزّل المثبّت ويتحقق من بصمته SHA-256 قبل تشغيله. لا يُرسل أي رقم أو بيانات.
- **الإبلاغ عن مشكلة أو اقتراح**: نموذج يفتح بلاغاً معبّأً في GitHub أو ينسخ التقرير، مع معاينة كاملة لما سيُرسل. لا يُرسل شيء تلقائياً.

### الاختصارات

| الاختصار | الوظيفة |
|---|---|
| <kbd>Ctrl</kbd>+<kbd>Alt</kbd>+<kbd>T</kbd> | تحويل الرقم في الحافظة من أي برنامج |
| <kbd>Ctrl</kbd>+<kbd>Enter</kbd> | نسخ النتيجة |
| <kbd>Esc</kbd> | مسح الرقم |
| <kbd>Ctrl</kbd>+<kbd>1</kbd> / <kbd>2</kbd> / <kbd>3</kbd> | تفقيط رقم · دفعة · الإعدادات |
| <kbd>Ctrl</kbd>+<kbd>V</kbd> | لصق عمود في «دفعة» |
| <kbd>F1</kbd> | حول البرنامج |

### التثبيت

من [صفحة الإصدارات](https://github.com/sarmadiq9090-coder/tafket/releases/latest):

- `InstaWords_<الإصدار>_x64-setup.exe`: المثبّت (موصى به). يثبّت للمستخدم الحالي في `%LOCALAPPDATA%\Programs\InstaWords` دون صلاحيات المسؤول، مع اختصار في قائمة ابدأ، واختياري على سطح المكتب ومع تشغيل Windows.
- `InstaWords_<الإصدار>_x64-portable.zip`: نسخة محمولة: فكّ الضغط وشغّل `InstaWords.exe`.

البرنامج غير موقّع رقمياً بعد، لذا قد يظهر تحذير SmartScreen: اختر «مزيد من المعلومات» ثم «تشغيل على أي حال».

الإعدادات والسجل في `%APPDATA%\FastKiwi\InstaWords` (ملفّا `settings.json` و`history.json`).

### البناء

انظر قسم «Build» بالإنجليزية أدناه.

</div>

## English

**InstaWords** is a **native** Windows app that writes amounts in words for cheques, invoices and receipts, in Arabic, English and French, with 100+ currencies and units. It is WPF on .NET Framework 4.8, which ships with Windows 10 and 11, so there is nothing else to install, no browser or WebView inside, and it works fully offline. Output is character-for-character identical to the website and the Excel function: the engine (`InstaWords.Core`) is generated from the same core.

### Features

- **Live conversion as you type**: Arabic-Indic (١٢٣) or Western digits, with or without thousands separators; the cleaned number («الرقم المعتمد») is shown with separators for checking.
- **Currency/unit picker** grouped by category, searchable in Arabic, by code (SAR) or English/French name: just type on it and press Enter.
- **Output language**: Arabic, English, French, each shown in its own text direction.
- **Formatting options**: مائة/مئة, round or reject extra decimals, prefix/suffix with per-language presets.
- **One-click copy** (or Ctrl+Enter) and a **history** of recent conversions, click to reuse.
- **Bulk tab**: paste a column from Excel or import CSV/TXT (header detection, quoted values), copy results one line per input, or export UTF-8 CSV (with BOM, formula-injection guarded). Smooth with 10,000 rows (virtualized grid, conversion off the UI thread).
- **Ctrl+Alt+T from any program**: copy a number, press the hotkey, and the clipboard now holds the words; a small toast shows the result. If another program owns the hotkey, the app says so and you pick another one in Settings.
- **Tray icon** (close/minimize to tray), single instance, optional start with Windows.
- **Updates**: at most once a day (can be turned off) the app asks the GitHub Releases API for the latest version; the installer is downloaded only from GitHub hosts and its SHA-256 is verified against the API digest before it runs silently and relaunches the app. Portable copies open the release page instead. Nothing about your numbers is ever sent.
- **Report a problem or suggestion**: builds a prefilled GitHub issue (opened in your browser for you to submit) or a report to copy, with a full preview. Nothing is sent automatically.

### Shortcuts

| Shortcut | Action |
|---|---|
| <kbd>Ctrl</kbd>+<kbd>Alt</kbd>+<kbd>T</kbd> | Convert the number in the clipboard, from any program |
| <kbd>Ctrl</kbd>+<kbd>Enter</kbd> | Copy the result |
| <kbd>Esc</kbd> | Clear the number |
| <kbd>Ctrl</kbd>+<kbd>1</kbd> / <kbd>2</kbd> / <kbd>3</kbd> | Single · Bulk · Settings |
| <kbd>Ctrl</kbd>+<kbd>V</kbd> | Paste a column on the Bulk tab |
| <kbd>F1</kbd> | About |

### Install

From [Releases](https://github.com/sarmadiq9090-coder/tafket/releases/latest): `InstaWords_<version>_x64-setup.exe` (per-user, no admin rights; Start menu shortcut, optional desktop shortcut and start with Windows; uninstaller) or `InstaWords_<version>_x64-portable.zip`. The app is not code-signed yet, so SmartScreen may warn: **More info → Run anyway**. Settings and history live in `%APPDATA%\FastKiwi\InstaWords`.

### Build

Requirements: .NET 8 SDK (and Node.js 22 for the engine generator). The app targets `net48` through the `Microsoft.NETFramework.ReferenceAssemblies` package, so it **compiles on Windows, Linux and macOS**; running it and the UI tests needs Windows.

```sh
npm ci && npm run test:dotnet                       # engine: generate, check, parity tests
dotnet test  windows/InstaWords.App.Tests           # app logic: updater, reports, CSV
dotnet build windows/InstaWords.App -c Release      # → windows/InstaWords.App/bin/Release/net48/InstaWords.exe
dotnet test  windows/InstaWords.UITests -c Release  # Windows only: FlaUI end-to-end tests + screenshots
iscc /DAppVersion=2.0.0 windows\installer\InstaWords.iss   # Inno Setup 6 → out\InstaWords_2.0.0_x64-setup.exe
```

| Path | What |
|---|---|
| `InstaWords.Core/` | The tafqit engine (netstandard2.0), generated from `src/` and `data/units.json` |
| `InstaWords.App/` | The WPF app: `Views/` (pages, dialogs, toast), `Themes/` (FastKiwi styles), `Services/` (hotkey, tray, clipboard, CSV, settings, updates, reports) |
| `InstaWords.App.Tests/` | Unit tests of the app's pure logic (links source files from the app) |
| `InstaWords.UITests/` | UI tests (FlaUI/UIA3). `INSTAWORDS_APP_PATH` and `INSTAWORDS_ARTIFACTS` override the exe and screenshot folder |
| `installer/InstaWords.iss` | Inno Setup script (Arabic + English) |

The version comes from the repo `package.json`. CI (`.github/workflows/windows.yml`) runs everything above on every push and uploads the installer, the portable zip and the screenshots; a manual run with **release** ticked publishes GitHub Release `v<version>`.

Test hooks (used only when set): `INSTAWORDS_DATA_DIR` (settings folder), `INSTAWORDS_UPDATE_FEED` (path to a release JSON instead of the GitHub API).

The embedded font is IBM Plex Sans Arabic (SIL Open Font License 1.1, `InstaWords.App/Fonts/OFL.txt`).
