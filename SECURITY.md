# Security Policy · سياسة الأمان

<div dir="rtl">

## بالعربية

### الإصدارات المدعومة

آخر إصدار منشور فقط هو الذي يتلقى إصلاحات الأمان. حدّث البرنامج قبل الإبلاغ، واذكر رقم الإصدار الذي جرّبته.

### كيف تُبلغ عن ثغرة

**لا تفتح بلاغًا عامًا (Issue) عن ثغرة أمنية.** أرسلها بشكل خاص بإحدى طريقتين:

- نموذج GitHub الخاص: [الإبلاغ عن ثغرة](https://github.com/sarmadiq9090-coder/InstaWords/security/advisories/new) (تبويب Security ← Report a vulnerability)؛
- أو البريد: support@fastkiwi.app

اكتب بالعربية أو بالإنجليزية.

### ماذا تكتب في البلاغ

- رقم إصدار InstaWords «تفقيط» وأي نسخة تستخدم (المثبّت، أو النسخة المحمولة، أو نسخة المتجر، أو وحدة Excel، أو الموقع)، وإصدار ويندوز؛
- ما الذي يستطيع المهاجم فعله، وما الذي يحتاجه لذلك؛
- خطوات إعادة المشكلة، أو مثالًا يثبتها.

### ما نرجو ألّا تفعله

- لا تنشر الثغرة علنًا (Issue أو منشور) قبل صدور الإصلاح.
- لا تصل إلى بيانات غيرك ولا تغيّرها؛ اختبر على جهازك وملفاتك أنت فقط.

### مواعيد الرد

يدير البرنامج شخص واحد بلا ميزانية، ولا توجد مكافآت مالية. ما نستطيع الالتزام به بصدق:

- تأكيد استلام البلاغ خلال 7 أيام؛
- للمشكلات الخطيرة: إصلاح، أو خطة إصلاح بموعد واضح، خلال 30 يومًا.

### الإفصاح المنسّق

نتفق معك على موعد النشر. بعد صدور الإصلاح ننشر تنبيهًا أمنيًا (advisory) ونذكر اسمك شكرًا لك إن رغبت.

### النطاق

- برنامج InstaWords «تفقيط» لويندوز (المثبّت، والنسخة المحمولة، ونسخة Microsoft Store)؛
- التحديث التلقائي المدمج فيه وملفات الإصدار التي ينزّلها؛
- وحدة Excel (`vba/Tafqit.bas`) وإضافة Excel؛
- صفحات موقعه: https://fastkiwi.app/instawords/

### خارج النطاق

- تحذير SmartScreen لأن المثبّت والنسخة المحمولة غير موقّعين: هذا اختيار معروف ومُعلن في [docs/CODE-SIGNING.md](docs/CODE-SIGNING.md).
- هجمات تتطلب أن يكون المهاجم قد شغّل برنامجه على الجهاز بحساب المستخدم نفسه.

</div>

## English

### Supported versions

Only the **latest release** receives security fixes. Please update before reporting, and mention
the version you tested.

### Reporting a vulnerability

**Please do not open a public issue for a security problem.** Report it privately, either:

- through GitHub: [**Report a vulnerability**](https://github.com/sarmadiq9090-coder/InstaWords/security/advisories/new)
  (repository → *Security* tab → *Report a vulnerability*), or
- by email: support@fastkiwi.app

Arabic or English is fine.

### What to include

- the InstaWords version, which build you use (installer, portable, Microsoft Store, Excel module,
  or the website) and the Windows version;
- what an attacker can do and what they need;
- steps to reproduce or a proof of concept.

### Please don't

- disclose the issue publicly (issues, posts) before a fix is released;
- access or change anyone else's data; test only on your own machine and files.

### What to expect

InstaWords is maintained by one person with no budget, and there is no bug bounty. What we can
honestly promise:

- an acknowledgement within **7 days**;
- for serious issues, a fix or a fix plan with a date within **30 days**.

### Coordinated disclosure

We agree a disclosure date with you. Once a fix is released we publish an advisory and credit you,
if you want to be credited.

### Scope

- the InstaWords Windows app (installer, portable and Microsoft Store builds);
- its built-in auto-updater and the release files it downloads;
- the Excel module (`vba/Tafqit.bas`) and Excel add-in;
- its website pages: https://fastkiwi.app/instawords/

### Out of scope

- SmartScreen warnings because the installer and portable build are not code-signed: a known
  choice, disclosed in [docs/CODE-SIGNING.md](docs/CODE-SIGNING.md).
- Attacks that need the attacker to already run code as the same Windows user.
