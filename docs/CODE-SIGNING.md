# التوقيع الرقمي · Code signing

<div dir="rtl">

## بالعربية

### لماذا؟

البرنامج والمثبّت غير موقّعين الآن، فيُظهر Windows تحذير SmartScreen («حمى Windows جهاز الكمبيوتر») عند أول تشغيل، ويرى المستخدم «ناشر غير معروف». التوقيع يضع اسم ناشر موثّق على الملف ويثبت أنه لم يتغير بعد بنائه. ملاحظة مهمة: التوقيع وحده لا يُخفي SmartScreen فوراً دائماً، فسمعة الشهادة والملف تُبنى مع عدد التنزيلات، لكنه شرط لبنائها.

### ما المُعدّ في المستودع

سير العمل `.github/workflows/windows.yml` فيه خطوات توقيع **اختيارية**:

- تعمل فقط في **تشغيل الإصدار** (Run workflow مع `release`) من الفرع الافتراضي، وفقط إذا كان المتغيّر `SIGNPATH_ORGANIZATION_ID` معرَّفاً. بدونه تُتخطى بهدوء ويُبنى كل شيء كما الآن دون توقيع.
- الرمز السري `SIGNPATH_API_TOKEN` محفوظ في بيئة GitHub محمية اسمها `signing` لا يدخلها إلا تشغيل الإصدار من الفرع الافتراضي، ولا تراه إلا خطوتا SignPath نفسيهما؛ فلا يصل إلى `npm ci` ولا إلى البناء والاختبارات، ولا إلى أي دفع (push) إلى فرع آخر.
- توقّع أولاً `InstaWords.exe` و`InstaWords.Core.dll` قبل بناء المثبّت والنسخة المحمولة (فهما يحملان الملفات الموقّعة)، ثم توقّع `InstaWords_<الإصدار>_x64-setup.exe`.
- خطوة «Verify signatures» تطبع حالة توقيع كل ملف، وتُفشل البناء إذا طُلب التوقيع ولم يكتمل. وملاحظات الإصدار تقول «موقّع» أو «غير موقّع» حسب النتيجة.
- البصمات (SHA-256) في الموقع وwinget وScoop والمحدِّث داخل البرنامج تُحسب من الملفات المنشورة، أي الموقّعة، فلا يلزم تغيير آخر.
- برنامج إلغاء التثبيت `unins000.exe` يولّده Inno Setup عند التثبيت، ولا يُوقَّع بهذه الطريقة (يحتاج `SignTool` داخل Inno، وهو خارج هذا الإعداد).

### الطريق الموصى به: SignPath Foundation (مجاني للمشاريع مفتوحة المصدر)

تمنح SignPath Foundation شهادة توقيع مجانية لمشاريع المصدر المفتوح، والتوقيع يتم على خدمة SignPath.io من مخرجات GitHub Actions مباشرة (المفتاح لا يغادر عتادهم الآمن). الشهادة باسم **SignPath Foundation**، فهذا هو اسم الناشر الذي يظهر في Windows، لا FastKiwi.

**الشروط الأساسية** (راجع النص الحالي في https://signpath.org/terms قبل التقديم):

- رخصة مفتوحة المصدر معتمدة من OSI (InstaWords: MIT ✔)، ودون أي مكوّن مغلق المصدر في الملفات الموقّعة.
- مشروع قائم ومُصان، وله إصدار منشور بالصيغة التي ستُوقَّع (✔ v2.0.0).
- لا برمجيات خبيثة أو غير مرغوب فيها، ووصف واضح لما يفعله البرنامج. اذكر في الصفحة أن البرنامج يتصل بـ GitHub فقط للتحقق من التحديثات (ويمكن إيقافه) ولا يرسل أي بيانات.
- البناء من المصدر على نظام بناء يمكن التحقق منه (GitHub Actions ✔).
- تفعيل التحقق بخطوتين (MFA) لكل أعضاء الفريق على GitHub وSignPath.
- صفحة أو قسم «سياسة التوقيع» في الموقع أو README (نص مقترح في آخر هذا الملف)، تذكر الأدوار (من يكتب الشيفرة، ومن يراجع، ومن يوافق على التوقيع).
- كل طلب توقيع للإصدار يحتاج **موافقة يدوية** من صاحب المشروع في SignPath (لذلك تنتظر الخطوة حتى ساعة).

**التقديم:**

1. املأ نموذج الطلب في https://signpath.org (Apply) بمعلومات المستودع `sarmadiq9090-coder/InstaWords`.
2. بعد القبول تحصل على منظمة (organization) في https://app.signpath.io. اربط مشروعاً بالمستودع، وأضف GitHub كنظام بناء موثوق (Trusted Build System) حسب تعليماتهم، ليتحقق SignPath أن الملفات جاءت من تشغيل GitHub Actions لهذا المستودع.
3. أنشئ في المشروع:
   - **سياسة توقيع** (signing policy)، مثلاً `release-signing`، مع موافقة يدوية.
   - **إعدادا ملفات** (artifact configurations):

     `app` (الملف المرفوع zip فيه البرنامج والمكتبة):

     ```xml
     <?xml version="1.0" encoding="utf-8"?>
     <artifact-configuration xmlns="http://signpath.io/artifact-configuration/v1">
       <zip-file>
         <pe-file path="InstaWords.exe"><authenticode-sign /></pe-file>
         <pe-file path="InstaWords.Core.dll"><authenticode-sign /></pe-file>
       </zip-file>
     </artifact-configuration>
     ```

     `setup`:

     ```xml
     <?xml version="1.0" encoding="utf-8"?>
     <artifact-configuration xmlns="http://signpath.io/artifact-configuration/v1">
       <zip-file>
         <pe-file path="InstaWords_*_x64-setup.exe"><authenticode-sign /></pe-file>
       </zip-file>
     </artifact-configuration>
     ```
4. أنشئ مستخدماً آلياً (CI user) في SignPath وانسخ رمزه (API token)، وامنحه صلاحية «submitter» على السياسة.
5. في GitHub ← Settings ← Environments ← New environment أنشئ بيئة باسم `signing`، وفي «Deployment branches and tags» اختر «Selected branches» وأضف الفرع الافتراضي وحده (ويُستحسن تفعيل «Required reviewers» بنفسك). ثم في هذه البيئة نفسها ← «Environment secrets» أضف السر `SIGNPATH_API_TOKEN` (لا تضعه في أسرار المستودع العامة).
6. وفي Settings ← Secrets and variables ← Actions ← Variables أضف:

| النوع | الاسم | القيمة |
|---|---|---|
| Secret (في بيئة `signing`) | `SIGNPATH_API_TOKEN` | رمز المستخدم الآلي |
| Variable | `SIGNPATH_ORGANIZATION_ID` | معرّف المنظمة (GUID) من SignPath؛ وجوده هو ما يفعّل التوقيع |
| Variable | `SIGNPATH_PROJECT_SLUG` | اختياري، الافتراضي `tafket` |
| Variable | `SIGNPATH_SIGNING_POLICY_SLUG` | اختياري، الافتراضي `release-signing` |
| Variable | `SIGNPATH_APP_ARTIFACT_CONFIGURATION_SLUG` | اختياري، الافتراضي `app` |
| Variable | `SIGNPATH_SETUP_ARTIFACT_CONFIGURATION_SLUG` | اختياري، الافتراضي `setup` |

7. شغّل الإصدار التالي كالعادة، ووافق على طلبي التوقيع في SignPath عندما يصلان. تحقق: زر الفأرة الأيمن على الملف ← خصائص ← التوقيعات الرقمية.

لإيقاف التوقيع مؤقتاً احذف المتغيّر `SIGNPATH_ORGANIZATION_ID`.

### البديل المدفوع

إن لم يُقبل الطلب أو أردت اسم FastKiwi ناشراً:

- **Azure Trusted Signing** من Microsoft (قد يظهر في بوابة Azure باسمه الأحدث Artifact Signing): اشتراك شهري، والتحقق من الهوية يتم عبر Microsoft، والتوقيع من CI مباشرة. الأهلية محدودة ببلدان وأنواع حسابات معينة، فتحقق من الشروط الحالية في صفحة الخدمة قبل الاعتماد عليها. يحتاج ذلك تغيير خطوتي التوقيع إلى إجراء Microsoft (`azure/trusted-signing-action`) بدل SignPath.
- **شهادة OV (Organization Validation)** من جهة إصدار معروفة (مثل DigiCert أو Sectigo أو SSL.com أو Certum، ولبعضها عروض خاصة بالمصدر المفتوح). منذ 2023 يجب أن يبقى المفتاح الخاص في عتاد آمن (مفتاح USB أو HSM أو خدمة توقيع سحابية لدى الجهة)، فللتوقيع من GitHub Actions اختر خدمة التوقيع السحابية لديهم. التوقيع يتم بـ `signtool sign /fd sha256 /tr <timestamp-url> /td sha256 ...`.
- الأسعار تتغير وتختلف حسب الجهة والمدة، فراجعها في مواقع الجهات مباشرة.

### نص مقترح لسياسة التوقيع (يُضاف إلى README أو صفحة الموقع بعد القبول)

> **سياسة التوقيع الرقمي:** التوقيع المجاني مقدَّم من [SignPath.io](https://about.signpath.io)، والشهادة من [SignPath Foundation](https://signpath.org).
> المطوّرون والمراجعون: أعضاء المستودع [sarmadiq9090-coder/InstaWords](https://github.com/sarmadiq9090-coder/InstaWords). الموافقة على التوقيع: صاحب المشروع.
> الخصوصية: لا يتصل البرنامج بأي جهة إلا GitHub للتحقق من وجود تحديث (يمكن إيقافه من الإعدادات)، ولا يُرسل أي رقم أو بيانات. الإبلاغ عن مشكلة يفتح صفحة GitHub في متصفحك ولا يُرسل شيئاً تلقائياً.

</div>

---

## English

**Now:** the app and installer are unsigned, so SmartScreen warns and shows "Unknown publisher". Signing names a verified publisher and proves the file is unchanged; SmartScreen reputation still builds up with downloads, but signing is the prerequisite.

**What is wired up** in `.github/workflows/windows.yml`: optional SignPath steps that run only on **release** runs from the default branch and only when the `SIGNPATH_ORGANIZATION_ID` variable is set (otherwise they are skipped and the build is unchanged). The `SIGNPATH_API_TOKEN` secret is an *environment* secret of the protected `signing` environment, which only those runs enter; only the two SignPath steps receive it, so `npm ci`, builds, tests and pushes to other branches never see it. `InstaWords.exe` and `InstaWords.Core.dll` are signed before the installer and portable zip are built, then the setup exe is signed. "Verify signatures" prints each file's status, fails the build if signing was requested but incomplete, and the release notes say signed/unsigned accordingly. All SHA-256 values (site, winget, Scoop, in-app updater) are computed from the published, i.e. signed, files. Inno's uninstaller (`unins000.exe`) is not signed this way (it needs Inno's `SignTool`).

**Recommended: SignPath Foundation** (free for open source). Certificates are issued to *SignPath Foundation*, which is the publisher name Windows shows. Requirements (check https://signpath.org/terms): OSI-approved licence (MIT ✔) and no proprietary components in what is signed, an existing maintained project with a released version (✔), no malware/PUA and a clear description (mention the update check to GitHub, which can be turned off, and that no data is sent), builds on a verifiable build system (GitHub Actions ✔), MFA for every team member on GitHub and SignPath, a code signing policy on the website/README (draft above, in Arabic: "Free code signing provided by SignPath.io, certificate by SignPath Foundation", roles, privacy), and a manual approval for each release signing request.

How to apply: submit the application at https://signpath.org; once accepted, in https://app.signpath.io create a project linked to `sarmadiq9090-coder/InstaWords` with GitHub as trusted build system, a signing policy (e.g. `release-signing`, manual approval), the artifact configurations `app` and `setup` (XML above), and a CI user with submitter rights. Then in GitHub → Settings → Environments create `signing` with Deployment branches = the default branch only (Required reviewers recommended) and add the token there as an environment secret; add the variables under Settings → Secrets and variables → Actions:

| Kind | Name | Value |
|---|---|---|
| Environment secret (`signing`) | `SIGNPATH_API_TOKEN` | CI user's API token |
| Variable | `SIGNPATH_ORGANIZATION_ID` | SignPath organization ID (its presence turns signing on) |
| Variable | `SIGNPATH_PROJECT_SLUG` | optional, default `tafket` |
| Variable | `SIGNPATH_SIGNING_POLICY_SLUG` | optional, default `release-signing` |
| Variable | `SIGNPATH_APP_ARTIFACT_CONFIGURATION_SLUG` | optional, default `app` |
| Variable | `SIGNPATH_SETUP_ARTIFACT_CONFIGURATION_SLUG` | optional, default `setup` |

Each signing step waits up to one hour for the approval. Remove the `SIGNPATH_ORGANIZATION_ID` variable to switch signing off.

**Paid alternatives** (to have FastKiwi as the publisher, or if the application is declined): **Azure Trusted Signing** (possibly shown as Artifact Signing in the Azure portal; monthly subscription; identity validated by Microsoft; eligibility is limited by country and account type, so check the current terms; swap the two SignPath steps for `azure/trusted-signing-action`), or an **OV code signing certificate** from a CA such as DigiCert, Sectigo, SSL.com or Certum (some have open-source offers). Since 2023 the private key must live in hardware (token, HSM or the CA's cloud signing service), so for GitHub Actions use the CA's cloud signing; sign with `signtool sign /fd sha256 /tr <timestamp-url> /td sha256`. Prices vary by CA and term: check the vendors directly.
