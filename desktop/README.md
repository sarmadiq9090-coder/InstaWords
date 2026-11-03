# InstaWords «تفقيط» for Windows

<div dir="rtl">

## بالعربية

**InstaWords** تطبيق Windows يكتب الأرقام والمبالغ بالحروف للشيكات والفواتير، بالعربية ولغات أخرى. هو الموقع نفسه (`_site/`) داخل نافذة [Tauri v2](https://v2.tauri.app/)، فنتائجه مطابقة للموقع حرفاً بحرف، ويعمل كله دون إنترنت.

### التنزيل

من صفحة [GitHub Releases](https://github.com/sarmadiq9090-coder/tafket/releases/latest):

- `InstaWords_<الإصدار>_x64-setup.exe`: المثبّت (موصى به). يثبّت للمستخدم الحالي فقط، ولا يطلب صلاحيات المسؤول.
- `InstaWords_<الإصدار>_x64-portable.exe`: نسخة محمولة تعمل دون تثبيت.

**تحذير SmartScreen:** التطبيق غير موقّع رقمياً بعد، لذا قد يعرض Windows رسالة «حمى Windows جهاز الكمبيوتر». اختر «مزيد من المعلومات» ثم «تشغيل على أي حال».

**WebView2:** يعتمد التطبيق على Microsoft Edge WebView2، وهو مثبّت مسبقاً في Windows 11 ومعظم أجهزة Windows 10. إن لم يكن موجوداً نزّله المثبّت تلقائياً (يحتاج اتصالاً بالإنترنت مرة واحدة). النسخة المحمولة لا تثبّته، فتحتاج جهازاً فيه WebView2.

### البناء

انظر قسم «Build» بالإنجليزية أدناه.

</div>

## English

**InstaWords** is a Windows app that writes numbers and amounts in words for cheques and invoices, in Arabic and other languages. It is the website (`_site/`) unchanged inside a [Tauri v2](https://v2.tauri.app/) window, so its output is identical to the site, and it works fully offline.

### Download

From [GitHub Releases](https://github.com/sarmadiq9090-coder/tafket/releases/latest):

- `InstaWords_<version>_x64-setup.exe`: installer (recommended). Per-user install, no admin rights.
- `InstaWords_<version>_x64-portable.exe`: portable, no install.

**SmartScreen:** the app is not code-signed yet, so Windows may show "Windows protected your PC". Click **More info → Run anyway**.

**WebView2:** the app renders with Microsoft Edge WebView2, preinstalled on Windows 11 and most Windows 10 PCs. The installer uses the WebView2 *download bootstrapper*: if the runtime is missing it downloads it (one-time, needs internet). This keeps the installer at a few MB; the alternative, the offline WebView2 installer, would add ~130 MB to every download for the few machines without it. The portable exe does not install WebView2, and like the installed app it keeps its WebView data in `%LOCALAPPDATA%\app.fastkiwi.instawords`.

### What differs from the website

Nothing in `site/` is modified. `src-tauri/src/init.js` is injected into every page and only adapts browser behaviour:

- the service worker is not registered (files are already local);
- external links (GitHub, fastkiwi.app, …) open in the default browser;
- downloads (the `Tafqit.bas` Excel module, CSV export) open a native **Save as** dialog.

The only IPC the page can use is the two Rust commands `save_file` and `open_external` (`src-tauri/capabilities/main.json`). Navigation to any non-app URL is blocked in Rust as well. The CSP is in `src-tauri/tauri.conf.json`.

### Build

Requirements (Windows): Node.js 22, Rust stable (MSVC), Visual Studio Build Tools with "Desktop development with C++", WebView2.

```sh
npm ci
npm run desktop:build   # builds _site/ (npm run build:site), then the app and the NSIS installer
npm run desktop:dev     # run the app from _site/ (re-run `npm run build:site` after editing site/)
```

Output in `desktop/src-tauri/target/release/`:

- `bundle/nsis/InstaWords_<version>_x64-setup.exe` (installer)
- `InstaWords.exe` (the portable exe; all site files are embedded in it)

The version comes from the repo `package.json`. CI (`.github/workflows/desktop.yml`) builds on every push and publishes a GitHub Release for `v*` tags (the tag must match `package.json`).

Icons are generated from `site/icon.svg`: `npm i --no-save playwright && npx playwright install chromium && node desktop/scripts/icons.mjs`.
