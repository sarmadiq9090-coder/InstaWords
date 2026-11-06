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
- **تبويب «عدة مبالغ»**: الصق عموداً من Excel أو استورد CSV/TXT (يتعرّف على سطر العناوين والقيم بين علامات التنصيص)، وانسخ النتائج سطراً لكل رقم، أو صدّرها CSV بترميز UTF-8 يفتحه Excel بالعربية. يتحمّل 10,000 صف دون تجمّد.
- **Ctrl+Alt+T من أي برنامج**: انسخ رقماً، اضغط الاختصار، فيصبح ما في الحافظة المبلغَ مكتوباً بالحروف مع إشعار صغير. يمكن تغيير الاختصار من الإعدادات إن كان برنامج آخر يستخدمه.
- **شريط المهام**: يبقى البرنامج بجانب الساعة عند الإغلاق (قابل للتغيير)، وقائمته: فتح، تحويل ما نسخته (الحافظة)، الإعدادات، التحقق من وجود تحديث، الإبلاغ عن مشكلة أو اقتراح، خروج. ونسخة واحدة فقط تعمل، و«تشغيل مع Windows» اختياري.
- **التحديث**: يتحقق مرة يومياً على الأكثر من صفحة الإصدارات في GitHub (يمكن إيقافه)، ويُنزّل المثبّت ويتحقق من بصمته SHA-256 قبل تشغيله. لا يُرسل أي رقم أو بيانات.
- **الإبلاغ عن مشكلة أو اقتراح**: نموذج يفتح بلاغاً معبّأً في GitHub أو ينسخ التقرير، مع معاينة كاملة لما سيُرسل. لا يُرسل شيء تلقائياً.
- **واجهة بالعربية أو الإنجليزية**: «لغة الواجهة» في الإعدادات: العربية أو English أو «تلقائي» (حسب لغة عرض Windows)، وتُطبَّق فوراً على كل النوافذ والقوائم والإشعارات ورسائل الأخطاء، من اليمين إلى اليسار بالعربية ومن اليسار إلى اليمين بالإنجليزية. لغة التفقيط (نص المبلغ) خيار مستقل. عند أول تشغيل بعد التثبيت تبدأ الواجهة بلغة معالج التثبيت.
- **طباعة الشيكات والسندات** (زر «طباعة» بجانب «نسخ»، أو Ctrl+P): أربعة قوالب بمعاينة حيّة هي نفسها ما يُطبع:
  - **شيك**: التاريخ والمستفيد والمبلغ بالأرقام في مربعه (‎#1,250.75#‎) والمبلغ بالحروف على سطرين مع خط متقطع يملأ ما بعده كما في الشيكات الحقيقية، وخط توقيع اختياري. يطبع القيم وحدها على شيك مطبوع مسبقاً، أو مع الإطار والعناوين على ورق عادي.
  - **سند قبض** و**سند صرف** على A5 أو A4: اسم الجهة وشعارها (من الإعدادات، اختياريان)، والرقم (يزيد تلقائياً بعد الطباعة)، والتاريخ، و«استلمنا من / يُصرف للسيد»، والمبلغ بالأرقام والحروف، والغرض، والتواقيع.
  - **شريط المبلغ**: المبلغ بالحروف وحده في إطار.
  - **المعايرة**: مقاسات جاهزة (شيك 175 × 80 مم وغيره) أو مقاس مخصص، وإزاحة X/Y بالمليمتر لكل حقل (ولكل الحقول معاً لهوامش الطابعة)، محفوظة في «ملفات شيكات» متعددة، و«طباعة تجريبية» لشبكة مليمترية تضعها فوق شيك حقيقي.
  - من تبويب «عدة مبالغ»: حدّد صفوفاً ثم «طباعة»، فيُطبع سند أو شيك لكل صف في صفحة مستقلة.
  - الطباعة عبر نافذة الطباعة في Windows، ولحفظ PDF اختر الطابعة «Microsoft Print to PDF». الكلمات تتبع خيارات التفقيط الحالية (اللغة والعملة والنص قبل المبلغ وبعده).

### الاختصارات

| الاختصار | الوظيفة |
|---|---|
| <kbd>Ctrl</kbd>+<kbd>Alt</kbd>+<kbd>T</kbd> | تحويل الرقم الذي نسخته (الحافظة)، من أي برنامج |
| <kbd>Ctrl</kbd>+<kbd>Enter</kbd> | نسخ النتيجة |
| <kbd>Ctrl</kbd>+<kbd>P</kbd> | طباعة شيك أو سند (أو الصفوف المحددة في «عدة مبالغ») |
| <kbd>Esc</kbd> | مسح الرقم |
| <kbd>Ctrl</kbd>+<kbd>1</kbd> / <kbd>2</kbd> / <kbd>3</kbd> | تفقيط رقم · عدة مبالغ · الإعدادات |
| <kbd>Ctrl</kbd>+<kbd>V</kbd> | لصق عمود في «عدة مبالغ» |
| <kbd>F1</kbd> | حول البرنامج |

### التثبيت

من [صفحة الإصدارات](https://github.com/sarmadiq9090-coder/tafket/releases/latest):

- `InstaWords_<الإصدار>_x64-setup.exe`: المثبّت (موصى به). يثبّت للمستخدم الحالي في `%LOCALAPPDATA%\Programs\InstaWords` دون صلاحيات المسؤول، مع اختصار في قائمة ابدأ، واختياري على سطح المكتب ومع تشغيل Windows.
- `InstaWords_<الإصدار>_x64-portable.zip`: نسخة محمولة: فكّ الضغط وشغّل `InstaWords.exe`.

البرنامج غير موقّع رقمياً بعد، لذا قد يظهر تحذير SmartScreen: اختر «مزيد من المعلومات» ثم «تشغيل على أي حال».

الإعدادات والسجل في `%APPDATA%\FastKiwi\InstaWords` (ملفّا `settings.json` و`history.json`). وإذا وقع خطأ غير متوقع كُتب في `crash.log` في المجلد نفسه (الوقت والإصدار ونص الخطأ فقط، بلا مبالغ)، ولا يُرسل إلى أي مكان؛ يمكنك إرفاقه بنفسك عند الإبلاغ عن مشكلة.

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
- **Arabic or English interface**: Settings → «لغة الواجهة · Interface language»: العربية, English or Automatic (the Windows display language). It applies live to every window, menu, toast and error message, right-to-left in Arabic and left-to-right in English. The tafqit output language (ar/en/fr) is a separate option. On the first run after installing, the interface starts in the language picked in the installer.
- **Cheque and voucher printing** («طباعة» / Print next to Copy, or Ctrl+P), four templates with a live preview that is exactly what prints:
  - **Cheque**: date, payee, the amount in figures in its box (`#1,250.75#`), the amount in words over two lines with a dashed filler after it like real cheques, and an optional signature line. Values only for pre-printed cheques, or with the frame and labels for plain paper.
  - **Receipt voucher** (سند قبض) and **payment voucher** (سند صرف) on A5 or A4: optional company name and logo (Settings), number (auto-incremented after printing), date, received from / paid to, amount in figures and words, purpose, signatures.
  - **Words strip**: just the amount in words, framed.
  - **Calibration**: paper presets (175 × 80 mm cheque and others) or a custom size, per-field X/Y offsets in mm (plus an "all fields" offset for printer margins) saved in named cheque profiles, and a **test print** of a millimetre grid with the field boxes to lay over a real cheque.
  - From the **Bulk** tab: select rows, then Print: one voucher or cheque per row, one per page.
  - Printing uses the Windows print dialog; choose **Microsoft Print to PDF** to save a PDF. The words use the current conversion options (language, unit, prefix/suffix).

### Shortcuts

| Shortcut | Action |
|---|---|
| <kbd>Ctrl</kbd>+<kbd>Alt</kbd>+<kbd>T</kbd> | Convert the number in the clipboard, from any program |
| <kbd>Ctrl</kbd>+<kbd>Enter</kbd> | Copy the result |
| <kbd>Ctrl</kbd>+<kbd>P</kbd> | Print a cheque or voucher (or the selected Bulk rows) |
| <kbd>Esc</kbd> | Clear the number |
| <kbd>Ctrl</kbd>+<kbd>1</kbd> / <kbd>2</kbd> / <kbd>3</kbd> | Single · Bulk · Settings |
| <kbd>Ctrl</kbd>+<kbd>V</kbd> | Paste a column on the Bulk tab |
| <kbd>F1</kbd> | About |

### Install

From [Releases](https://github.com/sarmadiq9090-coder/tafket/releases/latest): `InstaWords_<version>_x64-setup.exe` (per-user, no admin rights; Start menu shortcut, optional desktop shortcut and start with Windows; uninstaller) or `InstaWords_<version>_x64-portable.zip`. The app is not code-signed yet, so SmartScreen may warn: **More info → Run anyway**. Settings and history live in `%APPDATA%\FastKiwi\InstaWords`; unexpected errors (UI thread, background threads and unobserved tasks) are appended to `crash.log` there (time, version, exception and stack trace only, capped at 256 KB). It stays on your PC; attach it to a problem report if you like.

### Build

Requirements: .NET 8 SDK (and Node.js 22 for the engine generator). The app targets `net48` through the `Microsoft.NETFramework.ReferenceAssemblies` package, so it **compiles on Windows, Linux and macOS**; running it and the UI tests needs Windows.

```sh
npm ci && npm run test:dotnet                       # engine: generate, check, parity tests
dotnet test  windows/InstaWords.App.Tests           # app logic: updater, reports, CSV, strings, print layout
dotnet build windows/InstaWords.App -c Release      # → windows/InstaWords.App/bin/Release/net48/InstaWords.exe
dotnet test  windows/InstaWords.UITests -c Release  # Windows only: FlaUI end-to-end tests + screenshots
iscc /DAppVersion=2.2.0 windows\installer\InstaWords.iss   # Inno Setup 6 → out\InstaWords_2.2.0_x64-setup.exe
```

| Path | What |
|---|---|
| `InstaWords.Core/` | The tafqit engine (netstandard2.0), generated from `src/` and `data/units.json` |
| `InstaWords.App/` | The WPF app: `Views/` (pages, dialogs, toast, print dialog and renderer), `Themes/` (FastKiwi styles), `Strings/` (interface strings), `Services/` (hotkey, tray, clipboard, CSV, settings, updates, reports, print layout) |
| `InstaWords.App/Strings/` | `Strings.ar.xaml` and `Strings.en.xaml`: every interface string, same keys in both (a unit test checks the keys match, every key the app uses exists, English has no Arabic). XAML uses `{DynamicResource Key}`, code `Loc.T("Key")`; engine error codes map to `Err.CODE` in English |
| `InstaWords.App.Tests/` | Unit tests of the app's pure logic (links source files from the app): updates, reports, CSV, interface strings, print layout (mm ↔ DIP, offsets, word wrap and filler, template fields) |
| `InstaWords.UITests/` | UI tests (FlaUI/UIA3). `INSTAWORDS_APP_PATH` and `INSTAWORDS_ARTIFACTS` override the exe and screenshot folder |
| `installer/InstaWords.iss` | Inno Setup script (Arabic + English) |

The version comes from the repo `package.json`. CI (`.github/workflows/windows.yml`) runs everything above on every push and uploads the installer, the portable zip and the screenshots; a manual run with **release** ticked publishes GitHub Release `v<version>`.

Test hooks (used only when set): `INSTAWORDS_DATA_DIR` (settings folder), `INSTAWORDS_UPDATE_FEED` (path to a release JSON instead of the GitHub API).

The embedded font is IBM Plex Sans Arabic (SIL Open Font License 1.1, `InstaWords.App/Fonts/OFL.txt`).
