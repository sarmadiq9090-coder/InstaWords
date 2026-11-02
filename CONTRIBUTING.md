# دليل المساهمة · Contributing

<div dir="rtl">

شكراً لاهتمامك. هذا مشروع لغوي قبل أن يكون برمجياً، فالدقة أهم من السرعة.

## سياسة «الغرفة النظيفة»

- لا تنسخ شيفرة أو نصوصاً أو بيانات أو تصميماً من أي موقع أو منتج تفقيط آخر، ولا تكشط بياناته.
- كل قاعدة يجب أن تستند إلى قواعد العربية، ويُذكر مرجعها في طلب الدمج إن أمكن.

## أنواع المساهمة

### 1. إضافة وحدة أو تصحيحها

1. عدّل [`data/units.json`](data/units.json) وحده. يجب أن يطابق التعديل المخطط [`data/units.schema.json`](data/units.schema.json).
2. اكتب كل الصيغ صراحة: `singular` (مع 1، وبعد المئة والألف)، و`dual` (مع 2 وحده)، و`plural` (مع 3–10)، و`accusative` (مع 11–99)، و`gender`.
3. تنوين النصب يُكتب ألفاً تليها الفتحتان (`ريالاً`)، وما ينتهي بتاء مربوطة يُكتب بلا ألف (`هللة`). لا تضف أي تشكيل آخر.
4. إذا كانت الوحدة موصوفة فصِف كل صيغة: «ريالان سعوديان»، «ريالات سعودية».
5. الجزء الفرعي (`subunit`) نسبته قوة للعدد 10 فقط.
6. إن شككت في صيغة فاكتب الشك في `note`، ولا تخمّن.
7. شغّل `npm test && npm run build:vba && npm run test:vba`، وأرفق ملف `vba/Tafqit.bas` المُعاد توليده في طلب الدمج.

### 2. تعديل قاعدة لغوية

الترتيب إلزامي:

1. افتح نقاشاً (Issue) يشرح القاعدة ومرجعها، أو أشر إلى بند من القسم 16 في [`docs/SPEC.md`](docs/SPEC.md).
2. عدّل المواصفة `docs/SPEC.md`.
3. عدّل الحالات الذهبية [`tests/golden/cases.json`](tests/golden/cases.json) أو أضف إليها، فهي المرجع المشترك بين النواة وVBA.
4. عدّل النواة `src/`، ثم قالب VBA `vba/Tafqit.template.bas`، حتى تنجح `npm run test:all`.

### 3. الموقع

الملفات في `site/`، ولا يُسمح بأي مورد خارجي (لا CDN ولا خطوط من الويب) حفاظاً على العمل دون اتصال. اختبر بالأمر `npm run test:site`.

## المعايير

- النواة بلا أي اعتماديات وقت التشغيل، وتعمل في المتصفح وNode.
- الأرقام تُعالج نصوصاً، ولا يُستعمل `float` في المبالغ.
- مخرجات VBA تطابق النواة حرفاً بحرف، ويتحقق من ذلك `npm run test:vba`.
- ملفات `.bas` بترميز ASCII فقط، ونهايات أسطرها CRLF، ويولّدها السكربت.

</div>

## English summary

- **Clean-room:** never copy code, text, data or design from other tafqit products.
- **Add a unit:** edit only `data/units.json` with every form spelled out, keep it schema-valid, then run `npm test && npm run build:vba && npm run test:vba` and commit the regenerated `vba/Tafqit.bas`.
- **Change a rule:** issue → `docs/SPEC.md` → `tests/golden/cases.json` → `src/` → `vba/Tafqit.template.bas`, then `npm run test:all` must pass.
- **Site:** no external resources (offline-first); `npm run test:site`.
