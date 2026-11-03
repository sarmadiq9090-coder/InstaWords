# قائمة فحص على أجهزة حقيقية · Real-device test checklist

<div dir="rtl">

برنامج ويندوز لم يُشغَّل حتى الآن إلا على أجهزة GitHub الآلية. هذه القائمة لتجربته يدوياً كما يستخدمه الناس، مع Excel والموقع. ضع ✔ في المربع عند النجاح، وإن فشل بند فاكتب رقمه في البلاغ (انظر «الإبلاغ عن مشكلة» في الآخر).

## 0. التحضير

- **الأجهزة:** Windows 10 (22H2) وWindows 11 (23H2 أو أحدث). يكفي جهاز لكل نظام، وغيّر التكبير بينها.
- **التكبير:** الإعدادات ← النظام ← الشاشة ← المقياس: 100% ثم 125% ثم 150%. أعد تشغيل البرنامج بعد كل تغيير (وسجّل الخروج إن طلب Windows).
- **النسخ:** المثبّت والنسخة المحمولة لآخر إصدار من [صفحة الإصدارات](https://github.com/sarmadiq9090-coder/tafket/releases/latest)، ونسخة أقدم (مثل v2.0.0 من [كل الإصدارات](https://github.com/sarmadiq9090-coder/tafket/releases)) لتجربة التحديث.
- **البرامج:** Excel (Microsoft 365 أو 2016 فأحدث)، وWord، ومتصفح (Edge أو Chrome)، والمفكرة.
- سجّل في جدول النتائج آخر الملف: النظام ورقم البناء (`winver`)، والتكبير، ورقم إصدار البرنامج (F1 ← حول البرنامج).

مصفوفة التشغيل: نفّذ الأقسام 1 إلى 12 كاملة مرة على كل نظام بتكبير 100%، ثم كرر الأقسام 2 و3 و4 و6 (المظهر والنصوص) بتكبير 125% و150%.

| | 100% | 125% | 150% |
|---|---|---|---|
| Windows 10 | [ ] | [ ] | [ ] |
| Windows 11 | [ ] | [ ] | [ ] |

## 1. التنزيل والتثبيت

- [ ] **التنزيل من الموقع:** في https://fastkiwi.app/instawords/ اضغط «تنزيل برنامج ويندوز». ← يبدأ تنزيل `InstaWords_<الإصدار>_x64-setup.exe` مباشرة (دون المرور بصفحة GitHub)، ورقم الإصدار على الزر هو آخر إصدار.
- [ ] **البصمة (اختياري):** في PowerShell: `Get-FileHash .\InstaWords_<الإصدار>_x64-setup.exe`. ← تطابق SHA-256 المكتوبة في نافذة «ملاحظات الإصدار» بالموقع وفي `SHA256SUMS.txt`.
- [ ] **SmartScreen:** شغّل المثبّت. ← إن لم يكن موقّعاً: تظهر «حمى Windows جهاز الكمبيوتر»، و«مزيد من المعلومات» ← «تشغيل على أي حال» يكمل. إن كان موقّعاً: يظهر اسم الناشر في نافذة التحذير أو لا تظهر. اكتب ما ظهر بالضبط (صورة).
- [ ] **لا صلاحيات مسؤول:** ← لا تظهر نافذة UAC، والمثبّت بالعربية (أو الإنجليزية حسب لغة Windows).
- [ ] **الخيارات:** فعّل «إنشاء اختصار على سطح المكتب» واترك «التشغيل مع Windows» غير مفعّل. ← يثبّت في `%LOCALAPPDATA%\Programs\InstaWords`، ويظهر في قائمة ابدأ، وعلى سطح المكتب.
- [ ] **التطبيقات المثبّتة:** الإعدادات ← التطبيقات. ← «InstaWords — تفقيط»، الناشر FastKiwi، والإصدار الصحيح.

## 2. التشغيل الأول

- [ ] يفتح خلال ثانيتين تقريباً، دون رسائل خطأ، والنافذة كاملة داخل الشاشة (لا أزرار مقطوعة) بكل تكبير.
- [ ] الخط العربي واضح، والاتجاه من اليمين لليسار، ولا نص متداخل أو مقصوص في التبويبات الثلاثة (تفقيط رقم، عدة مبالغ، الإعدادات).
- [ ] يعمل دون إنترنت: افصل الشبكة ثم شغّله. ← كل التحويلات تعمل، ولا رسالة خطأ.
- [ ] تشغيله مرة ثانية (من قائمة ابدأ) لا يفتح نسخة ثانية، بل يُظهر النافذة الموجودة.
- [ ] الوضع الداكن (الإعدادات ← التخصيص ← الألوان ← داكن)، إن كان البرنامج يتبعه: النصوص مقروءة.

## 3. التفقيط بثلاث لغات

اكتب الرقم في تبويب «تفقيط رقم»، واختر العملة واللغة. النتيجة المتوقعة حرفياً:

| | الرقم | الوحدة | اللغة | النتيجة المتوقعة |
|---|---|---|---|---|
| [ ] | `1250.75` | ريال سعودي (SAR) | العربية | ألف ومائتان وخمسون ريالاً سعودياً وخمس وسبعون هللة |
| [ ] | `١٢٣٫٤٥` (أرقام عربية) | دينار كويتي (KWD) | العربية | مائة وثلاثة وعشرون ديناراً كويتياً وأربعمائة وخمسون فلساً |
| [ ] | `2500` مع «فقط … لا غير» | SAR | العربية | فقط ألفان وخمسمائة ريال سعودي لا غير |
| [ ] | `300` مع «مئة» | SAR | العربية | ثلاثمئة ريال سعودي |
| [ ] | `1000000` | بدون وحدة | العربية | مليون |
| [ ] | `1250.75` | SAR | English | One Thousand Two Hundred Fifty Saudi Riyals and Seventy-Five Halalas |
| [ ] | `0.5` | USD | English | Fifty Cents |
| [ ] | `1250.75` | EUR | Français | mille deux cent cinquante euros et soixante-quinze centimes |
| [ ] | `1250` | MAD | Français | mille deux cent cinquante dirhams marocains |

- [ ] **فواصل الآلاف:** `1,250.75` و`1٬250٫75` ← النتيجة نفسها (ألف ومائتان وخمسون ريالاً سعودياً وخمس وسبعون هللة)، و«الرقم المعتمد» يظهر الرقم منقّى بفواصل الآلاف.
- [ ] **الأخطاء:** `abc` ← «صيغة الرقم غير صحيحة.»؛ `12.345` مع SAR وخيار «إظهار خطأ» ← رسالة أن الخانات العشرية أكثر من المسموح؛ ومع «تقريب» ← اثنا عشر ريالاً سعودياً وخمس وثلاثون هللة.
- [ ] **البحث عن عملة:** في قائمة العملات اكتب `دينار` ثم `KWD` ثم `euro`. ← تظهر العملات المطابقة، وEnter يختار.
- [ ] **اتجاه النص:** الناتج الإنجليزي والفرنسي من اليسار لليمين، والعربي من اليمين لليسار.
- [ ] **النسخ:** زر النسخ ثم Ctrl+Enter، والصق في المفكرة. ← النص نفسه دون رموز غريبة.
- [ ] **السجل:** آخر التحويلات تظهر، والنقر على أحدها يعيده.
- [ ] تطابق الموقع: الرقم نفسه في https://fastkiwi.app/instawords/ يعطي النص نفسه حرفاً بحرف.

## 4. عدة مبالغ: اللصق من Excel

- [ ] في Excel اكتب عموداً: عنوان «المبلغ» ثم `100` و`2500.5` و`١٢٣` وخلية فارغة و`abc`. انسخ العمود والصقه في تبويب «عدة مبالغ» (Ctrl+V). ← يتعرّف على سطر العنوان، ولكل رقم نتيجة، والخلية الفارغة والقيمة الخاطئة معلّمتان دون توقف.
- [ ] **ألف صف أو أكثر:** في Excel اكتب `=RANDBETWEEN(1,1000000)/100` في A1:A10000، انسخ والصق. ← النتائج تظهر خلال ثوانٍ، والنافذة لا تتجمّد، والتمرير سلس.
- [ ] **نسخ النتائج** والصقها في عمود B بجانب الأرقام. ← سطر لكل رقم، بالترتيب نفسه.
- [ ] **تصدير CSV** وافتحه بالنقر المزدوج في Excel. ← العربية سليمة (ليست رموزاً مثل Ø§Ù„)، والأعمدة صحيحة.
- [ ] **استيراد CSV/TXT** فيه قيم بين علامات تنصيص. ← يُقرأ صحيحاً.

## 5. Ctrl+Alt+T من أي برنامج

لكل برنامج: انسخ رقماً (Ctrl+C)، اضغط Ctrl+Alt+T، ثم الصق (Ctrl+V).

- [ ] **Excel:** خلية فيها `1250.75`. ← يظهر إشعار صغير بالنتيجة، واللصق يعطي المبلغ بالحروف.
- [ ] **Word:** رقم محدد داخل فقرة. ← كذلك، والنص العربي يُلصق باتجاه صحيح.
- [ ] **المتصفح:** رقم من صفحة ويب أو من مربع نص. ← كذلك.
- [ ] **حافظة ليست رقماً** (نص عادي). ← إشعار بأن المحتوى ليس رقماً، والحافظة لا تتغير.
- [ ] **البرنامج مصغّر في شريط المهام أو بجانب الساعة**: الاختصار يعمل.
- [ ] **تعارض الاختصار:** إن كان برنامج آخر يستخدم Ctrl+Alt+T يقول البرنامج ذلك، وتغيير الاختصار من الإعدادات يعمل فوراً.

## 6. الأيقونة بجانب الساعة (Tray)

- [ ] إغلاق النافذة (×) يبقي البرنامج بجانب الساعة (حسب الإعداد)، والنقر على الأيقونة يعيد النافذة.
- [ ] قائمة الأيقونة (زر أيمن): فتح، تحويل الحافظة، الإعدادات، التحقق من وجود تحديث، الإبلاغ عن مشكلة أو اقتراح، خروج. ← كل بند يعمل، و«خروج» ينهي البرنامج فعلاً (لا يبقى في مدير المهام).
- [ ] بتكبير 150%: الأيقونة واضحة غير مشوشة، والقائمة مقروءة.

## 7. التشغيل مع Windows

- [ ] فعّل «تشغيل مع Windows» من الإعدادات، وأعد تشغيل الجهاز. ← يبدأ البرنامج بجانب الساعة دون فتح نافذة.
- [ ] أوقفه من الإعدادات وأعد التشغيل. ← لا يبدأ. (وفي إدارة المهام ← بدء التشغيل لا تبقى قيمة InstaWords مفعّلة.)

## 8. لغة الواجهة والطباعة (إن كانت في هذا الإصدار)

- [ ] تغيير لغة الواجهة إلى English من الإعدادات. ← كل النصوص بالإنجليزية والاتجاه من اليسار لليمين، دون نص مقصوص، ويبقى الاختيار بعد إعادة التشغيل.
- [ ] طباعة شيك: معاينة الطباعة تُظهر المبلغ بالحروف والرقم في مكانهما، وضبط المحاذاة يحرّكهما، والطباعة على ورق A4 (أو شيك حقيقي) مطابقة للمعاينة. وتصدير PDF (Microsoft Print to PDF) يعمل.

## 9. التحديث من إصدار أقدم

- [ ] ثبّت نسخة أقدم (مثلاً v2.0.0) وغيّر بعض الإعدادات (اللغة، العملة) وأنشئ سجلاً.
- [ ] من قائمة الأيقونة ← «التحقق من وجود تحديث». ← يظهر الإصدار الجديد مع ملاحظاته.
- [ ] «تحديث الآن». ← يُنزَّل المثبّت ويُتحقق منه، ويُغلق البرنامج ويُثبَّت بصمت ويعود للعمل وحده. رقم الإصدار في «حول البرنامج» جديد.
- [ ] الإعدادات والسجل باقية كما كانت (`%APPDATA%\FastKiwi\InstaWords`).
- [ ] دون إنترنت: «التحقق من وجود تحديث» يعطي رسالة واضحة، دون تجمّد.
- [ ] النسخة المحمولة: التحقق من التحديث يفتح صفحة الإصدار بدل التثبيت.
- [ ] (بديل) تثبيت المثبّت الجديد يدوياً فوق القديم. ← يحدّث دون سؤال عن مجلد، والبرنامج يعمل.

## 10. الإبلاغ عن مشكلة من داخل البرنامج

- [ ] قائمة الأيقونة ← «الإبلاغ عن مشكلة أو اقتراح». ← نموذج فيه معاينة كاملة لما سيُرسل، وفيه رقم الإصدار ونسخة Windows، ولا أرقام ولا سجل ولا محتوى الحافظة.
- [ ] «فتح في GitHub» يفتح بلاغاً معبّأً في المتصفح، و«نسخ التقرير» ينسخه. لا يُرسل شيء تلقائياً.

## 11. النسخة المحمولة

- [ ] فكّ `InstaWords_<الإصدار>_x64-portable.zip` في مجلد عادي (مثل سطح المكتب) وشغّل `InstaWords.exe`. ← يعمل دون تثبيت، وفيه `portable.txt`.
- [ ] من مجلد على قرص USB، ومن مسار فيه حروف عربية أو مسافات. ← يعمل.

## 12. إلغاء التثبيت

- [ ] الإعدادات ← التطبيقات ← InstaWords ← إلغاء التثبيت، والبرنامج يعمل بجانب الساعة. ← يُغلق ويُزال، وتختفي اختصارات قائمة ابدأ وسطح المكتب، ولا تبقى قيمة «التشغيل مع Windows».
- [ ] المجلد `%LOCALAPPDATA%\Programs\InstaWords` محذوف. (الإعدادات في `%APPDATA%\FastKiwi\InstaWords` قد تبقى: اكتب هل بقيت.)
- [ ] إعادة التثبيت بعدها تعمل.

## 13. Excel (VBA)

على Excel لـ Windows (وMac إن توفر):

- [ ] نزّل `Tafqit.bas` من https://fastkiwi.app/instawords/excel.html. افتح مصنفاً جديداً ← Alt+F11 ← File ← Import File… ← `Tafqit.bas` (و`TafqitTests.bas` من المستودع للاختبار الذاتي). احفظ بصيغة ‎.xlsm وأعد فتحه وفعّل الماكرو. ← لا رسائل خطأ، والوحدة باسم `TafqitLib`.
- [ ] في A1 اكتب `1250.75` ثم جرّب:

| | الصيغة | النتيجة المتوقعة |
|---|---|---|
| [ ] | `=TAFQIT(A1, "SAR")` | ألف ومائتان وخمسون ريالاً سعودياً وخمس وسبعون هللة |
| [ ] | `=TAFQIT(2500, "ريال سعودي", "مائة", "فقط", "لا غير")` | فقط ألفان وخمسمائة ريال سعودي لا غير |
| [ ] | `=TAFQIT(1500.5, "KWD")` | ألف وخمسمائة دينار كويتي وخمسمائة فلس |
| [ ] | `=TAFQIT(300, "SAR", "مئة")` | ثلاثمئة ريال سعودي |
| [ ] | `=TAFQIT(A1, "SAR", "", "", "", "", "en")` | One Thousand Two Hundred Fifty Saudi Riyals and Seventy-Five Halalas |
| [ ] | `=TAFQIT(1250, "SAR", "", "", "Only", "", "en")` | One Thousand Two Hundred Fifty Saudi Riyals Only |
| [ ] | `=TAFQIT(1250, "MAD", , , , , "fr")` | mille deux cent cinquante dirhams marocains |
| [ ] | `=TAFQIT(A1, "EUR", , , , , "FR")` (أحرف كبيرة) | mille deux cent cinquante euros et soixante-quinze centimes |
| [ ] | `=TAFQIT(12.345, "SAR", , , , "error")` | عدد الخانات العشرية أكبر مما تسمح به الوحدة. |
| [ ] | `=TAFQIT(5, "XYZ")` | الوحدة غير معروفة. |
| [ ] | `=TAFQIT_CLEAN("١٢٣٫٤٥")` | 123.45 |

- [ ] اسحب الصيغة على عمود من 1,000 رقم. ← تُحسب خلال ثوانٍ.
- [ ] **الاختبار الذاتي:** Alt+F8 ← `TafqitSelfTest` ← تشغيل. ← رسالة «InstaWords self-test: N of N golden cases passed.». إن ظهر عدد فاشل: Ctrl+G في المحرر وانسخ ما في نافذة Immediate إلى البلاغ.
- [ ] نسخة Excel ولغتها (عربية/إنجليزية) مكتوبة في جدول النتائج.

## 14. الموقع

على Edge أو Chrome في Windows، وعلى هاتف (Android أو iPhone):

- [ ] **أزرار التنزيل** (أعلى الصفحة، قسم ويندوز، الأسئلة الشائعة، أسفل الصفحة، وفي صفحتي «دفعة» وExcel): كلها تنزّل المثبّت مباشرة، والرقم عليها آخر إصدار.
- [ ] **نافذة «ملاحظات الإصدار»** (رابط «ما الجديد»): رقم الإصدار والتاريخ والحجم وبصمة SHA-256 وزر المثبّت وزر النسخة المحمولة وملاحظات الإصدار. Esc يغلقها والتركيز يعود للرابط.
- [ ] **كل الإصدارات:** رابط GitHub الوحيد الواضح في النافذة يفتح صفحة الإصدارات.
- [ ] **التفقيط في الموقع:** أمثلة القسم 3 تعطي النتائج نفسها، وصفحة «دفعة» تقبل اللصق من Excel وتصدّر CSV.
- [ ] **دون إنترنت:** افتح الموقع مرة، ثم افصل الإنترنت (أو وضع الطيران) وأعد تحميل الصفحة. ← تفتح وتعمل التحويلات، ونافذة الإصدار تقول إن GitHub غير متاح وتعرض الإصدار المضمّن في الموقع مع رابط التنزيل المباشر.
- [ ] **الإبلاغ عن مشكلة** (رابط أسفل الصفحة): نافذة داخل الموقع تشرح الخيارات وفيها زر GitHub واحد.
- [ ] **الهاتف (عرض 375 بكسل):** لا تمرير أفقي، والأزرار قابلة للضغط، والنص مقروء.

## 15. winget وScoop (بعد نشرهما)

- [ ] `winget install Sarmad.InstaWords` ← يثبّت بصمت لكل مستخدم، والبرنامج يعمل. و`winget upgrade Sarmad.InstaWords` بعد إصدار جديد يحدّثه. `winget uninstall Sarmad.InstaWords` يزيله.
- [ ] `scoop install fastkiwi/instawords` ← اختصار «InstaWords» في قائمة ابدأ، والأمر `InstaWords` يفتحه. `scoop uninstall instawords` يزيله.

## الإبلاغ عن مشكلة

لكل بند فاشل افتح بلاغاً بإحدى الطرق:

1. **من داخل البرنامج:** قائمة الأيقونة بجانب الساعة ← «الإبلاغ عن مشكلة أو اقتراح» (يضيف رقم الإصدار ونسخة Windows تلقائياً).
2. **من الموقع:** رابط «الإبلاغ عن مشكلة» أسفل أي صفحة.
3. **مباشرة:** نموذج «الإبلاغ عن مشكلة» في https://github.com/sarmadiq9090-coder/tafket/issues/new/choose

واكتب فيه: رقم البند في هذه القائمة، وWindows ورقم بنائه (`winver`)، والتكبير، ورقم إصدار البرنامج (F1)، وExcel ونسخته إن كان الخطأ فيه، وخطوات إعادة المشكلة، والنتيجة المتوقعة والفعلية، وصورة للشاشة (Win+Shift+S).

## جدول النتائج

| التاريخ | المختبِر | Windows (winver) | التكبير | إصدار البرنامج | Excel | الأقسام المنجزة | البنود الفاشلة (أرقام البلاغات) |
|---|---|---|---|---|---|---|---|
| | | | | | | | |

</div>

---

## English summary

Manual checks of the Windows app, the Excel module and the website on real hardware (so far the app has only run on GitHub's CI machines). Tick each box when the expected result is seen; report failures with the item number.

- **Setup**: Windows 10 22H2 and Windows 11; display scaling 100%, 125%, 150% (restart the app after each change). Sections 1 to 12 in full on each OS at 100%; sections 2, 3, 4 and 6 again at 125% and 150%. Have an older release (e.g. v2.0.0) for the update test, plus Excel, Word, a browser and Notepad.
- **1. Install**: the site's download button downloads the setup directly with the latest version; optional `Get-FileHash` matches the site/`SHA256SUMS.txt`; SmartScreen behaviour recorded (unsigned: More info → Run anyway); no UAC; per-user install to `%LOCALAPPDATA%\Programs\InstaWords`; Apps & features shows "InstaWords — تفقيط", publisher FastKiwi.
- **2. First run**: fast start, nothing clipped at any scaling, RTL layout, works offline, single instance.
- **3. Conversions**: the table above gives the exact expected text in Arabic, English and French (e.g. `1250.75` SAR → "One Thousand Two Hundred Fifty Saudi Riyals and Seventy-Five Halalas"), plus thousands separators, error messages, currency search, copy, history, identical to the website.
- **4. Bulk**: paste a column from Excel (header, blank, invalid cells), 10,000 rows without freezing, copy results back, CSV export opens in Excel with intact Arabic, CSV import.
- **5. Ctrl+Alt+T** in Excel, Word and a browser (toast + clipboard replaced), non-numbers left alone, works while minimised, hotkey conflict handled.
- **6. Tray**, **7. Start with Windows** (on and off, after a reboot), **8. UI language / cheque printing** if the build has them.
- **9. Update** from an older version: check for update, update now, silent install and relaunch, settings and history kept; offline message; portable opens the release page.
- **10. In-app report** shows a full preview and sends nothing automatically. **11. Portable zip** from USB and paths with Arabic/spaces. **12. Uninstall** removes shortcuts and the Run value.
- **13. Excel VBA**: import `Tafqit.bas` (+ `TafqitTests.bas`), save as .xlsm; the `=TAFQIT` examples (including the 7th `Language` argument "en"/"fr", case-insensitive), `TAFQIT_CLEAN`, and `TafqitSelfTest` → "N of N golden cases passed".
- **14. Website**: all download buttons are direct, the release-notes dialog (version, date, size, SHA-256, portable, notes), the releases link, offline behaviour after one visit (conversions work, dialog shows the bundled release), the support dialog, phone layout at 375 px.
- **15. winget / Scoop** once published.
- **Reporting**: in the app (tray → report a problem or suggestion), the site footer's report link, or the issue form at https://github.com/sarmadiq9090-coder/tafket/issues/new/choose. Include the item number, `winver`, scaling, app version (F1), Excel version, steps, expected vs actual, and a screenshot. Fill in the results table.
