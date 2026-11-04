# إصدار نسخة جديدة · Releasing InstaWords

<div dir="rtl">

## بالعربية

رقم الإصدار مكتوب في مكان واحد يُعتمد عليه: `package.json`. منه يأخذ CI رقم برنامج ويندوز والمثبّت ووسم الإصدار `v<الإصدار>`. أما روابط التنزيل الاحتياطية في الموقع فمصدرها الوحيد `site/release-fallback.json`، ولا تُحدَّث إلا **بعد** نشر الإصدار، لأنها يجب أن تشير إلى ملف موجود فعلاً.

### الخطوات

1. **التحضير** (على جهازك):

   ```sh
   npm run release:prepare -- 2.1.0
   ```

   يرفع الرقم في `package.json` و`package-lock.json`، والرقم الافتراضي لبناء البرنامج محلياً (`windows/InstaWords.App/InstaWords.App.csproj`)، وأمثلة البناء في `windows/README.md` و`windows/installer/InstaWords.iss`، ثم يطبع قائمة الخطوات التالية. أضف `--dry-run` لترى التغييرات دون كتابة شيء.

   ثم أضف قسماً `## <الإصدار>` في [`CHANGELOG.md`](CHANGELOG.md) بما تغيّر (بالعربية ثم الإنجليزية). ينسخه سير العمل إلى ملاحظات الإصدار تحت «الجديد في هذا الإصدار»، ويفشل الاختبار `tests/changelog.test.ts` وتشغيل النشر إن غاب القسم.

2. **الاختبارات**: `npm run test:all` (و`npm run test:dotnet` إن كان ‎.NET SDK مثبتاً).
3. **الحفظ والرفع**: `git commit -am "Release v2.1.0"` ثم `git push`، وانتظر نجاح سير العمل «Windows (native app)» على هذا الإيداع (البناء، اختبارات الواجهة، الصور).
4. **النشر**: في GitHub ← Actions ← «Windows (native app)» ← Run workflow، وفعّل الخيار **release**. ينشئ CI الوسم `v2.1.0` وإصدار GitHub بستة ملفات:
   - `InstaWords_2.1.0_x64-setup.exe` و`InstaWords_2.1.0_x64-portable.zip` (لا تغيّر هذين الاسمين: المحدِّث داخل البرنامج يبحث عنهما)،
   - `InstaWords.xlam` (إضافة Excel، يبنيها `scripts/build-xlam.mjs` من `vba/Tafqit.bas`)،
   - `SHA256SUMS.txt` (بصمات الملفات الثلاثة)،
   - `winget-manifests.zip` و`instawords.json` (ملفات winget وScoop معبّأة بالبصمات الحقيقية، انظر [`docs/PACKAGING.md`](docs/PACKAGING.md)).

   وإن كان التوقيع الرقمي مُعدّاً (السر `SIGNPATH_API_TOKEN`) يُوقَّع البرنامج والمثبّت قبل النشر، انظر [`docs/CODE-SIGNING.md`](docs/CODE-SIGNING.md).
5. **المراجعة**: افتح صفحة الإصدار وتأكد من الملفات الخمسة والملاحظات، وجرّب التحديث من نسخة أقدم ([`docs/TESTING-CHECKLIST.md`](docs/TESTING-CHECKLIST.md)).
6. **روابط الموقع الاحتياطية** بعد النشر:

   ```sh
   npm run release:fallback   # يحدّث site/release-fallback.json من آخر إصدار، ثم packaging/
   npm run test:site
   git commit -am "Site fallback: v2.1.0" && git push
   ```

   السكربت `scripts/update-release-fallback.mjs` يقرأ آخر إصدار من واجهة GitHub (ويقبل `GITHUB_TOKEN` اختيارياً)، ويختار الملفين بالاسم، ويأخذ SHA-256 من بصمة GitHub لكل ملف (`digest`)، ولا يكتب إلا إذا كان الإصدار أحدث. دون إنترنت ينتهي بنجاح مع رسالة ولا يغيّر شيئاً.
   وتلتقط المزامنة اليومية لموقع FastKiwi الإصدار الجديد أيضاً، فهذه الخطوة تضمن أن المستودع نفسه محدّث.
7. **winget وScoop**: اتبع [`docs/PACKAGING.md`](docs/PACKAGING.md).

### أين يظهر رقم الإصدار الاحتياطي؟

كلها تُشتق من `site/release-fallback.json` عند بناء الموقع (`scripts/build-site.mjs`)، فلا تعدّلها يدوياً:

- روابط أزرار التنزيل في `site/*.html` وأرقامها (`data-windows-download` و`data-version`). المصدر قد يبقى على v2.0.0، والموقع المبني `_site/` يحمل ما في الملف.
- `downloadUrl` في JSON-LD بالصفحة الرئيسية.
- `FALLBACK` في `site/assets/release.js` (يُكتب مكان `/* @release-fallback */ null`).
- `STATIC_EXE` في اختبارات الموقع `tests/site/smoke.mjs`.
- نسخ winget وScoop المحفوظة في `packaging/` (`node scripts/package-manifests.mjs`).

### إصدار تجريبي

رقم مثل `2.1.0-rc.1` يُنشر إصداراً تجريبياً (pre-release). المحدِّث داخل البرنامج لا يعرضه، و`releases/latest` لا يعيده، فلا يتغير الموقع. لا ترسله إلى winget أو Scoop.

</div>

---

## English

The version lives in one authoritative place, `package.json`: CI takes the Windows app version, the installer version and the release tag `v<version>` from it. The website's fallback download links come from one file, `site/release-fallback.json`, which is refreshed only **after** the release is published, because it must name a file that exists.

### Steps

1. **Prepare** (locally): `npm run release:prepare -- 2.1.0` bumps `package.json` and `package-lock.json`, the app project's default `<Version>` (local builds; CI passes `-p:Version`), and the build examples in `windows/README.md` and `windows/installer/InstaWords.iss`, then prints the checklist. `--dry-run` shows the changes without writing. Then add a `## <version>` section to [`CHANGELOG.md`](CHANGELOG.md) (Arabic, then English): the workflow copies it into the release notes under "What's new", and both `tests/changelog.test.ts` and the release run fail without it.
2. **Test**: `npm run test:all` (and `npm run test:dotnet` with the .NET SDK).
3. **Commit and push**, then wait for the "Windows (native app)" workflow on that commit to pass.
4. **Publish**: Actions → "Windows (native app)" → Run workflow → tick **release**. CI creates tag `v2.1.0` and the GitHub release with six assets: the setup exe and the portable zip (keep these names, the in-app updater looks for them), the Excel add-in `InstaWords.xlam`, `SHA256SUMS.txt`, and `winget-manifests.zip` + `instawords.json` (winget and Scoop manifests with the real SHA-256, see [`docs/PACKAGING.md`](docs/PACKAGING.md)). With the `SIGNPATH_API_TOKEN` secret configured, the app and the installer are signed first ([`docs/CODE-SIGNING.md`](docs/CODE-SIGNING.md)).
5. **Review** the release page; try updating from an older version ([`docs/TESTING-CHECKLIST.md`](docs/TESTING-CHECKLIST.md)).
6. **Website fallback**, after publishing: `npm run release:fallback` (refreshes `site/release-fallback.json` from the latest release, then `packaging/`), `npm run test:site`, commit and push. `scripts/update-release-fallback.mjs` reads the GitHub API (optional `GITHUB_TOKEN`), picks the assets by name, takes the SHA-256 from each asset's `digest`, and writes only when the release is newer; offline it exits 0 with a message. The FastKiwi site's daily sync also picks up new releases; this step keeps the repository itself current.
7. **winget and Scoop**: see [`docs/PACKAGING.md`](docs/PACKAGING.md).

Everything derived from `site/release-fallback.json` at build time (do not edit by hand): the download buttons' hrefs and version labels in `site/*.html` (the sources may keep v2.0.0; `_site/` carries the JSON's release), the JSON-LD `downloadUrl`, `FALLBACK` in `site/assets/release.js` (written over `/* @release-fallback */ null`), `STATIC_EXE` in `tests/site/smoke.mjs`, and the committed winget/Scoop copies in `packaging/`.

Pre-releases (`2.1.0-rc.1`) are published as GitHub pre-releases: the in-app updater ignores them and `releases/latest` does not return them, so the website is unchanged. Do not submit them to winget or Scoop.
