# التوقيع الرقمي · Code signing

<div dir="rtl">

## بالعربية

**المثبّت والنسخة المحمولة غير موقّعين رقمياً، وهذا مقصود** (قرار D-052: أدوات مجانية فقط). لذلك قد يُظهر Windows تحذير SmartScreen عند أول تشغيل: اختر «مزيد من المعلومات» ثم «تشغيل على أي حال». ملاحظات كل إصدار تقول ذلك، وبصمات SHA-256 لكل ملف في `SHA256SUMS.txt` مع الإصدار.

**الطريق الموقّع هو متجر Microsoft:** حزمة MSIX تُبنى في CI غير موقّعة (الملف المؤقت `store-package`)، ويوقّعها المتجر عند النشر. التفاصيل في [`docs/distribution/microsoft-store.md`](distribution/microsoft-store.md).

أُزيلت خطوات SignPath السابقة من `.github/workflows/windows.yml`، فلا يحتاج سير العمل أي سر أو متغيّر للتوقيع، ولا البيئة `signing`. إن كانت هذه البيئة أو المتغيّرات `SIGNPATH_*` أو السر `SIGNPATH_API_TOKEN` موجودة في إعدادات المستودع فيمكن لصاحب المشروع حذفها (Settings ← Environments، وSettings ← Secrets and variables ← Actions).

</div>

---

## English

**The installer and the portable zip are not code-signed, on purpose** (decision D-052: free tools only), so Windows SmartScreen may warn on first run: "More info" → "Run anyway". Every release's notes say so, and `SHA256SUMS.txt` in each release has the SHA-256 of every file.

**The signed path is the Microsoft Store:** CI builds the MSIX unsigned (artifact `store-package`) and the Store signs it when it publishes. See [`docs/distribution/microsoft-store.md`](distribution/microsoft-store.md).

The former SignPath steps were removed from `.github/workflows/windows.yml`: the workflow needs no signing secret, variable or `signing` environment. If the `signing` environment, `SIGNPATH_*` variables or the `SIGNPATH_API_TOKEN` secret still exist in the repository settings, the owner can delete them (Settings → Environments; Settings → Secrets and variables → Actions).
