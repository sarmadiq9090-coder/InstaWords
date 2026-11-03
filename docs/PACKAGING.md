# النشر في winget وScoop · Publishing to winget and Scoop

<div dir="rtl">

## بالعربية

### الملفات

| الملف | الاستخدام |
|---|---|
| `packaging/winget/templates/` | قوالب winget (المخطط 1.12.0، أربعة ملفات: version وdefaultLocale en-US وlocale ar-SA وinstaller) |
| `packaging/winget/manifests/s/Sarmad/InstaWords/<الإصدار>/` | ملفات winget معبّأة لآخر إصدار منشور، جاهزة للإرسال |
| `packaging/scoop/instawords.json` | ملف Scoop لآخر إصدار منشور (النسخة المحمولة) |
| `scripts/package-manifests.mjs` | يولّد كل ما سبق (`npm run packaging`) من `site/release-fallback.json`، أو من الملفات المبنية في CI |
| ملفات الإصدار `winget-manifests.zip` و`instawords.json` و`SHA256SUMS.txt` | يرفعها CI مع كل إصدار، بالبصمات الحقيقية للملفات المنشورة |

**المعرّف:** `Sarmad.InstaWords`، على نمط `Sarmad.InstaFind` في winget، ليجتمع البرنامجان تحت ناشر واحد في winget-pkgs. وحقل `Publisher` هو `FastKiwi` لأنه ما يكتبه المثبّت في «التطبيقات المثبّتة» (`AppPublisher` في `InstaWords.iss`).

**المثبّت:** Inno Setup لكل مستخدم (`InstallerType: inno`، `Scope: user`، دون صلاحيات المسؤول)، و`ProductCode` هو `{8F3B6C2A-5D4E-4A7B-9C1F-2E6D8A0B4C71}_is1` (معرّف `AppId` في Inno مع `_is1`)، و`UpgradeBehavior: install` (التحديث يثبّت فوق النسخة الموجودة كما يفعل المحدِّث داخل البرنامج). لا أوامر سطر أوامر (`Commands`) لأن المثبّت لا يضيف شيئاً إلى PATH.

### winget: أول إرسال

مرة واحدة: وقّع اتفاقية Microsoft CLA (يطلبها البوت في أول طلب دمج).

```powershell
winget install Microsoft.WingetCreate
wingetcreate token --store                     # رمز GitHub كلاسيكي بصلاحية public_repo
$m = "packaging\winget\manifests\s\Sarmad\InstaWords\2.0.0"
winget validate --manifest $m
winget settings --enable LocalManifestFiles    # كمسؤول، مرة واحدة
winget install --manifest $m                   # تجربة محلية
winget uninstall Sarmad.InstaWords
wingetcreate submit $m
```

`submit` ينشئ نسخة (fork) من `microsoft/winget-pkgs` ويرفع فرعاً ويفتح طلب الدمج بعنوان مثل `New package: Sarmad.InstaWords version 2.0.0`.

### winget: الإصدارات التالية

إما أن تنزّل `winget-manifests.zip` من صفحة الإصدار وتفكّه ثم:

```powershell
wingetcreate submit manifests\s\Sarmad\InstaWords\2.1.0
```

أو تترك wingetcreate يحدّث آخر ملف مقبول:

```powershell
wingetcreate update Sarmad.InstaWords --version 2.1.0 --urls https://github.com/sarmadiq9090-coder/tafket/releases/download/v2.1.0/InstaWords_2.1.0_x64-setup.exe --submit
```

**ما يفحصه winget-pkgs:** المخطط والروابط، ويتحقق من SHA-256، ويفحص الملف بعدة برامج مكافحة فيروسات، ويثبّته في جهاز نظيف. البرامج غير الموقّعة مقبولة، لكن أي إنذار من برنامج مكافحة يرفض الطلب: عندها أرسل الملف بلاغاً خاطئاً إلى https://www.microsoft.com/wdsi/filesubmission مع رابط الطلب. بعد الدمج يعمل `winget install Sarmad.InstaWords` أو `winget install instawords`.

**التحديث الذاتي:** المحدِّث داخل البرنامج يشغّل المثبّت نفسه، فيبقى سجل «التطبيقات المثبّتة» صحيحاً وwinget يرى الإصدار الجديد عبر `ProductCode`. لا تعارض بينهما.

### Scoop

الدلو (bucket) الخاص بصاحب المشروع هو مجلد `bucket/` في مستودع InstaFind (`sarmadiq9090-coder/arabic-file-search`)، وفيه `instafind.json`. أضف `instawords.json` إلى المجلد نفسه:

1. انسخ `packaging/scoop/instawords.json` (أو `instawords.json` من صفحة الإصدار) إلى `arabic-file-search/bucket/instawords.json`، واحفظه وارفعه.
2. التثبيت عند المستخدمين:

   ```powershell
   scoop bucket add fastkiwi https://github.com/sarmadiq9090-coder/arabic-file-search
   scoop install fastkiwi/instawords
   ```

   (اسم الدلو يختاره المستخدم؛ من أضافه سابقاً باسم `instafind` يكتب `scoop install instafind/instawords`.)
3. التحديث: الملف فيه `checkver: github` و`autoupdate` (الرابط بالإصدار، والبصمة من `SHA256SUMS.txt` في الإصدار نفسه)، فأبسط طريق نسخ `instawords.json` الجديد من صفحة الإصدار إلى `bucket/`. ومن يستخدم أدوات Scoop يمكنه تشغيل `checkver.ps1 instawords -dir bucket -u` (من مجلد `bin` في Scoop) ليكتب الإصدار والبصمة تلقائياً.

تجربة محلية قبل الرفع: `scoop install .\packaging\scoop\instawords.json`. يثبّت النسخة المحمولة مع اختصار «InstaWords» في قائمة ابدأ وأمر `InstaWords`. الإعدادات في `%APPDATA%\FastKiwi\InstaWords` خارج مجلد Scoop، وإلغاء التثبيت يحذف قيمة «التشغيل مع Windows» إن كانت تشير إلى مجلد Scoop.

**دلو Extras الرسمي** يشترط شهرة كافية (نحو 100 نجمة أو 50 نسخة للمستودع)، كما في `arabic-file-search/packaging/SUBMITTING.md`. حتى ذلك الحين يكفي الدلو الخاص.

</div>

---

## English

### Files

| File | Use |
|---|---|
| `packaging/winget/templates/` | winget templates (schema 1.12.0; version, defaultLocale en-US, locale ar-SA, installer) |
| `packaging/winget/manifests/s/Sarmad/InstaWords/<version>/` | Filled manifests of the latest published release, ready to submit |
| `packaging/scoop/instawords.json` | Scoop manifest of the latest published release (portable zip) |
| `scripts/package-manifests.mjs` | Generates the above (`npm run packaging`) from `site/release-fallback.json`, or from the built files in CI |
| Release assets `winget-manifests.zip`, `instawords.json`, `SHA256SUMS.txt` | Attached by CI to every release, with the published files' real SHA-256 |

**Identifier** `Sarmad.InstaWords`, matching InstaFind's `Sarmad.InstaFind`, so both apps share one publisher folder in winget-pkgs. `Publisher` is `FastKiwi`, which is what the installer writes to Apps & features (`AppPublisher` in `InstaWords.iss`).

**Installer**: per-user Inno Setup (`InstallerType: inno`, `Scope: user`, no admin rights), `ProductCode` `{8F3B6C2A-5D4E-4A7B-9C1F-2E6D8A0B4C71}_is1` (the Inno `AppId` + `_is1`), `UpgradeBehavior: install`, no `Commands` (nothing is added to PATH). `/CLOSEAPPLICATIONS` is passed so a copy running in the tray is closed instead of prompting.

### winget

Once: sign the Microsoft CLA (the bot asks on the first PR). First submission:

```powershell
winget install Microsoft.WingetCreate
wingetcreate token --store                     # classic GitHub PAT with public_repo
$m = "packaging\winget\manifests\s\Sarmad\InstaWords\2.0.0"
winget validate --manifest $m
winget settings --enable LocalManifestFiles    # as admin, once
winget install --manifest $m; winget uninstall Sarmad.InstaWords
wingetcreate submit $m
```

Later versions: unzip the release's `winget-manifests.zip` and run `wingetcreate submit manifests\s\Sarmad\InstaWords\<version>`, or `wingetcreate update Sarmad.InstaWords --version <v> --urls <setup url> --submit`.

The validation pipeline checks the schema and URLs, verifies the SHA-256, scans with several antivirus engines and installs in a clean VM. Unsigned apps are accepted; any antivirus hit rejects the PR (report a false positive at https://www.microsoft.com/wdsi/filesubmission). The in-app updater runs the same installer, so Apps & features and winget stay in agreement through the `ProductCode`.

### Scoop

The owner's bucket is the `bucket/` folder of the InstaFind repository (`sarmadiq9090-coder/arabic-file-search`, next to `instafind.json`). Copy `packaging/scoop/instawords.json` (or the release asset `instawords.json`) to `arabic-file-search/bucket/instawords.json` and push. Users then run:

```powershell
scoop bucket add fastkiwi https://github.com/sarmadiq9090-coder/arabic-file-search
scoop install fastkiwi/instawords
```

The manifest has `checkver: github` and `autoupdate` (versioned URL; hash read from the release's `SHA256SUMS.txt`), so a later version needs only the new release asset copied into `bucket/`, or Scoop's own `checkver.ps1 instawords -dir bucket -u`. Test locally first with `scoop install .\packaging\scoop\instawords.json`. It installs the portable build (Start menu shortcut "InstaWords", `InstaWords` command); settings stay in `%APPDATA%\FastKiwi\InstaWords`; uninstalling removes the start-with-Windows value if it points into the Scoop folder. The official Extras bucket requires a well-known project (about 100 stars or 50 forks), see `arabic-file-search/packaging/SUBMITTING.md`.
