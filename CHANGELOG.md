# Changelog · سجل التغييرات

Each version's section is copied into its GitHub release notes by the release workflow
(`scripts/changelog-section.mjs`). Earlier versions: [GitHub Releases](https://github.com/sarmadiq9090-coder/InstaWords/releases).

## Unreleased

<div dir="rtl">

- **نسخة Microsoft Store**: بعد خامس مرة تنسخ فيها مبلغاً بالحروف يسألك البرنامج مرة واحدة فقط «هل يساعدك InstaWords؟» ويفتح نافذة التقييم الخاصة بالمتجر إن اخترت «قيّم في المتجر»، ولا يسأل بعدها أبداً أيّاً كان جوابك. وفي «حول» رابط دائم «قيّم InstaWords في المتجر». لا يظهر في المثبّت ولا في النسخة المحمولة، ولا يُرسل شيئاً.

</div>

- **Microsoft Store copy**: after the fifth amount in words you copy, the app asks once, "Is InstaWords helping you?", and "Rate in the Store" opens the Store's own rating dialog; whatever you answer, it never asks again. About also has a permanent "Rate InstaWords in the Store" link. Not in the installer or portable builds; nothing is sent.

## 2.2.0

<div dir="rtl">

- **إضافة Excel ‏`InstaWords.xlam`**: ثبّتها مرة واحدة فتصبح الدالة `=TAFQIT` متاحة في كل مصنف دون إنترنت، بلا استيراد وحدة VBA في كل ملف. صفحة Excel في الموقع تشرح التثبيت و«إلغاء الحظر» و«المواقع الموثوقة». (مبنية وفق المواصفات ومختبرة آلياً، وتحتاج تجربة في Excel حقيقي.)
- **سجل الأعطال**: أي خطأ غير متوقع، في الواجهة أو في الخلفية، يُكتب في `crash.log` داخل `%APPDATA%\FastKiwi\InstaWords` (الوقت والإصدار ونص الخطأ فقط، بلا مبالغ)، ولا يُرسل إلى أي مكان. أرفقه بنفسك عند الإبلاغ عن مشكلة.
- **أمان البناء**: رمز التوقيع الرقمي لا يصل إلا إلى خطوتي التوقيع في تشغيل الإصدار، داخل بيئة GitHub محمية.

</div>

- **Excel add-in `InstaWords.xlam`**: install once and `=TAFQIT` works in every workbook, offline, without importing the VBA module into each file. The site's Excel page covers installing, Unblock and Trusted Locations. (Built to spec and verified by parsing; needs a real-Excel smoke test.)
- **Crash log**: unexpected errors, on the UI thread or in the background, are written to `crash.log` in `%APPDATA%\FastKiwi\InstaWords` (time, version and the error only, no amounts). Nothing is sent; attach it to a problem report if you like.
- **Build security**: the code-signing token reaches only the two signing steps of a release run, inside a protected GitHub environment.
