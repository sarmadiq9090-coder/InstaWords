# مواصفة التفقيط — الإصدار 1

> المنتج: **InstaWords**. اللغة الافتراضية العربية (الأقسام 0–16)، والإنجليزية والفرنسية في القسم 17.
>
> الحالة: **الإصدار 1 — منفَّذ**. البنود [Rn] و[Pn] محسومة حسب عرف المالية والحسابات (القسم 16). كل قاعدة هنا مأخوذة من قواعد العدد والمعدود في العربية الفصحى أو من متطلبات المشروع. ما لم يُحسم لغوياً موسوم بالرمز **[Rn]** ومشروح في القسم 16، وقيمته الحالية افتراض مؤقت لا قاعدة نهائية.

---

## 0. المصطلحات

| الرمز | المعنى |
|---|---|
| `N` | الجزء الصحيح من المبلغ (يُعالج نصاً ثم `BigInt`) |
| `S` | قيمة الجزء الفرعي (هللة، فلس…) بعد التحويل، `0 ≤ S < R` |
| `R` | نسبة الجزء الفرعي: قوة للعدد 10 (`100` أو `1000`…) |
| `d` | عدد خانات الكسر المسموح = `log10(R)` |
| `G` | جنس المعدود: `m` مذكر، `f` مؤنث |
| `k` | المرتبة: 0 آحاد، 1 ألوف، 2 ملايين، 3 مليارات، 4 تريليونات |
| `g` | قيمة مجموعة من ثلاث خانات (`0..999`) |
| `r` | آخر خانتين من عدد: `r = x mod 100` |

**الحدّ الأقصى للجزء الصحيح:** `999 999 999 999 999` (خمس مراتب). ما فوقه خطأ `TOO_LARGE`.

---

## 1. تنقية المدخلات

تُطبّق الخطوات بالترتيب على النص المدخل:

| # | الخطوة | مثال |
|---|---|---|
| 1 | حذف المسافات بأنواعها (`U+0020`، `U+00A0`، `U+2009`، `U+202F`) من البداية والنهاية والوسط | `" 1 500 "` ← `"1500"` |
| 2 | تحويل الأرقام الهندية `٠–٩` (`U+0660–0669`) والفارسية `۰–۹` (`U+06F0–06F9`) إلى `0–9` | `"١٢٣"` ← `"123"` |
| 3 | حذف فواصل الآلاف: `,` و`٬` (`U+066C`) **[P3]** | `"2,500"` ← `"2500"` |
| 4 | توحيد الفاصلة العشرية: `٫` (`U+066B`) ← `.` | `"123٫45"` ← `"123.45"` |
| 5 | التحقق بالتعبير `^(\d+(\.\d*)?|\.\d+)$`؛ وإلا فالخطأ `INVALID_FORMAT`. الإشارة السالبة مرفوضة `NEGATIVE` **[P4]** | `"12a"` ← خطأ |
| 6 | حذف الأصفار البادئة من الجزء الصحيح (والفارغ يصبح `"0"`) | `"007"` ← `"7"` |
| 7 | حذف الأصفار الزائدة في نهاية الكسر | `"15.00"` ← `"15"`، `"5.250"` ← `"5.25"` |

**الرقم بعد التنقية** (الذي يُعرض في جدول التفقيط المتعدد) = الجزء الصحيح، يليه `.` والكسر إن بقي منه شيء: `"١٢٣٫٤٥"` ← `"123.45"`.

**تحويل الكسر إلى جزء فرعي:** إن كان طول الكسر بعد الخطوة 7 أكبر من `d` يُقرَّب تقريباً نصفياً للأعلى بحساب نصي إلى `d` خانات (`10.125` ← `10.13`، و`0.999` ← `1`)، أو يُرجَع الخطأ `TOO_MANY_DECIMALS` إذا اختير `decimals: "error"` **[P1]**. ثم يُكمَّل الكسر بأصفار من اليمين حتى `d` خانات، ويُقرأ عدداً صحيحاً: `"0.5"` مع `R=100` ← `S=50`، و`"1500.125"` مع `R=1000` ← `S=125`.

لا تُستخدم الأعداد العشرية (`float`) في أي خطوة.

---

## 2. ألفاظ الأعداد

### 2.1 من 1 إلى 10

| العدد | مع مذكر | مع مؤنث |
|---|---|---|
| 1 | واحد | واحدة |
| 2 | اثنان | اثنتان |
| 3 | ثلاثة | ثلاث |
| 4 | أربعة | أربع |
| 5 | خمسة | خمس |
| 6 | ستة | ست |
| 7 | سبعة | سبع |
| 8 | ثمانية | ثماني **[R5]** |
| 9 | تسعة | تسع |
| 10 | عشرة | عشر |

1 و2 توافقان المعدود، ومن 3 إلى 10 تخالفه.

### 2.2 من 11 إلى 19

| العدد | مع مذكر | مع مؤنث |
|---|---|---|
| 11 | أحد عشر | إحدى عشرة |
| 12 | اثنا عشر | اثنتا عشرة |
| 13 | ثلاثة عشر | ثلاث عشرة |
| 14 | أربعة عشر | أربع عشرة |
| 15 | خمسة عشر | خمس عشرة |
| 16 | ستة عشر | ست عشرة |
| 17 | سبعة عشر | سبع عشرة |
| 18 | ثمانية عشر | ثماني عشرة |
| 19 | تسعة عشر | تسع عشرة |

11 و12 توافقان في الجزأين، ومن 13 إلى 19 يخالف الجزء الأول ويوافق الثاني. الصيغ مرفوعة (اثنا/اثنتا).

### 2.3 العقود والأعداد المعطوفة (21–99)

| 20 | 30 | 40 | 50 | 60 | 70 | 80 | 90 |
|---|---|---|---|---|---|---|---|
| عشرون | ثلاثون | أربعون | خمسون | ستون | سبعون | ثمانون | تسعون |

العقود لا تتغير بالجنس. العدد المعطوف = `آحاد + " و" + عقد`، والآحاد فيه:
- مع المذكر: كالجدول 2.1 (واحد وعشرون، اثنان وعشرون، ثلاثة وعشرون…).
- مع المؤنث: `1 ← إحدى` **[R4]**، وسائر الآحاد كالجدول 2.1 (اثنتان وعشرون، ثلاث وعشرون، ثماني وعشرون **[R5]**).

### 2.4 المئات (خيار `hundredsForm`)

| | `"مائة"` (الافتراضي) | `"مئة"` |
|---|---|---|
| 100 | مائة | مئة |
| 200 مستقلة | مائتان | مئتان |
| 200 قبل لفظ مرتبة (قسم 6) | مائتا | مئتا |
| 300 | ثلاثمائة | ثلاثمئة |
| 400 | أربعمائة | أربعمئة |
| 500 | خمسمائة | خمسمئة |
| 600 | ستمائة | ستمئة |
| 700 | سبعمائة | سبعمئة |
| 800 | ثمانمائة | ثمانمئة |
| 900 | تسعمائة | تسعمئة |

المئات لا تتغير بالجنس، وتُكتب من 300 إلى 900 متصلة.

### 2.5 المراتب

| k | مفرد | مثنى | جمع (3–10) | مفرد منصوب (11–99) |
|---|---|---|---|---|
| 1 | ألف | ألفان | آلاف | ألفاً |
| 2 | مليون | مليونان | ملايين | مليوناً |
| 3 | مليار | ملياران | مليارات | ملياراً |
| 4 | تريليون | تريليونان | تريليونات | تريليوناً |

ألفاظ المراتب مذكرة، فالأعداد قبلها تُعامل دائماً معاملة المذكر (ثلاثة آلاف، أحد عشر ألفاً، واحد وعشرون مليوناً).

**الإملاء:** تنوين النصب يُكتب ألفاً تليها الفتحتان `ا + U+064B` (ألفاً، ريالاً). ما ينتهي بتاء مربوطة يُكتب بلا ألف ولا تشكيل (هللة، ليرة). لا يُضاف أي تشكيل آخر **[R12]**.

---

## 3. لفظ مجموعة `words(g, G)` حيث `1 ≤ g ≤ 999`

```
h = floor(g / 100) ، r = g mod 100
الأجزاء = []
إن h > 0: أضف لفظ المئات h (جدول 2.4، الصيغة المستقلة)
إن 1 ≤ r ≤ 10:  أضف جدول 2.1 [G][r]
إن 11 ≤ r ≤ 19: أضف جدول 2.2 [G][r]
إن 20 ≤ r ≤ 99: u = r mod 10 ، t = floor(r / 10)
                 u = 0 ← العقد t
                 وإلا ← آحاد معطوفة [G][u] + " و" + العقد t
النتيجة = الأجزاء مربوطة بـ " و"
```

أمثلة: `125, m` ← «مائة وخمسة وعشرون»، `75, f` ← «خمس وسبعون»، `250, m` ← «مائتان وخمسون».

---

## 4. لفظ مرتبة `scaleTerm(g, k)` حيث `k ≥ 1` و`g ≥ 1`

| الشرط | الناتج | مثال |
|---|---|---|
| `g = 1` | المفرد | ألف، مليون |
| `g = 2` | المثنى | ألفان |
| `g ≥ 3` و`r = 0` | `words(g, m)` + المفرد؛ و«مائتان» تصير «مائتا» لأنها مضافة | مائة ألف، مائتا ألف، ثلاثمائة مليون |
| `g ≥ 3` و`r ∈ {1, 2}` | `words(g, m)` + المفرد **[R7]** | مائة وواحد ألف |
| `3 ≤ r ≤ 10` | `words(g, m)` + الجمع | ثلاثة آلاف، مائة وعشرة آلاف |
| `11 ≤ r ≤ 99` | `words(g, m)` + المنصوب (أو المفرد إن أضيف، قسم 6) | أحد عشر ألفاً، مائة وعشرون مليوناً |

---

## 5. تمييز المعدود (الوحدة)

يُحدَّد التمييز بآخر جزء من `N`، أي بـ `r = N mod 100`:

| الحالة | الصيغة | النص | مثال (ريال، مذكر) | مثال (ليرة، مؤنث) |
|---|---|---|---|---|
| `N = 0` | — | «صفر» + المفرد **[R6]** | صفر ريال | صفر ليرة |
| `N = 1` | — | الحقل `one` إن وُجد، وإلا المفرد + «واحد/واحدة» **[R10]** | ريال واحد | ليرة واحدة |
| `N = 2` | — | المثنى وحده | ريالان | ليرتان |
| `N > 2` و`r = 0` | مفرد مجرور | `words` + المفرد | مائة ريال، ألف ريال | مائة ليرة |
| `N > 2` و`r ∈ {1, 2}` | مفرد | `words` + المفرد **[R1]** | مائة وواحد ريال | مائة وواحدة ليرة |
| `3 ≤ r ≤ 10` | جمع مجرور | `words` + الجمع | ثلاثة ريالات، مائة وعشرة ريالات | ثلاث ليرات |
| `11 ≤ r ≤ 99` | مفرد منصوب | `words` + المنصوب | أحد عشر ريالاً | إحدى عشرة ليرة |

الجنس `G` يُطبَّق على المجموعة `k = 0` فقط، والمراتب الأعلى مذكرة (قسم 2.5).

الصيغ (المفرد، المثنى، الجمع، المنصوب، الجنس) تُقرأ من ملف الوحدات كما هي، ولا يُشتق شيء منها في الكود. فإن كان الاسم موصوفاً (ريال سعودي) فالصيغ تحمل الصفة مطابقة: «ريالان سعوديان»، «ريالات سعودية»، «ريالاً سعودياً».

---

## 6. الإضافة (حذف نون المثنى وتنوين النصب)

يكون اللفظ **مضافاً** إذا تلاه اسم مباشرة بلا واو:

1. **«مائتان/مئتان» قبل لفظ مرتبة:** تصير «مائتا/مئتا» دائماً: مائتا ألف، مائتا مليون.
2. **آخر لفظ في `N` قبل اسم الوحدة**، إذا كانت هناك وحدة وكانت الحالة في القسم 5 من حالات `N > 2`:
   - المثنى يبقى بنونه حسب عرف المالية والحسابات: «ألفان ريال»، «مليونان ريال» **[R2]**.
   - منصوب المرتبة ← المفرد: «أحد عشر ألف ريال»، «مائة وعشرون مليون ريال» **[R3]**.
   - «مائتان» في آخر `N` تبقى «مائتان»: «مائتان ريال» **[R2]**.
3. **بدون وحدة** لا إضافة في الحالة 2: «ألفان»، «مائتان»، «أحد عشر ألفاً».

---

## 7. الربط

- تُربط المراتب غير الصفرية من الأعلى إلى الأدنى بـ `" و"`، والواو متصلة بما بعدها: «مليون وألف وخمسة».
- المراتب الصفرية تُحذف ولا تُذكر.
- بين العدد واسم الوحدة مسافة واحدة.
- لا مسافات مكررة ولا في الطرفين.

نص الجزء الصحيح:
```
groups = تقسيم N إلى مجموعات من ثلاث خانات من اليمين
النص = [scaleTerm(g_k, k) لكل k ≥ 1 من الأعلى، ثم words(g_0, G) إن g_0 > 0]
       مربوطة بـ " و"، ثم اسم الوحدة بحسب القسم 5، مع تطبيق القسم 6
```

---

## 8. الكسور (الجزء الفرعي)

- لكل وحدة جزء فرعي اختياري له صيغه وجنسه ونسبته `R` في ملف البيانات. القيم الحالية: `100` للريال السعودي، و`1000` للدينار العراقي والكويتي والبحريني والأردني والريال العماني.
- يُفقَّط `S` بالقواعد نفسها (الأقسام 3–6) بصيغ الجزء الفرعي وجنسه.
- التركيب:

| `N` | `S` | الناتج |
|---|---|---|
| أي قيمة | `0` | نص `N` وحده (ومنه «صفر ريال») |
| `0` | `> 0` | نص `S` وحده: «خمسون هللة» |
| `> 0` | `> 0` | نص `N` + `" و"` + نص `S` |

- وحدة بلا جزء فرعي ومعها كسر غير صفري: الخطأ `FRACTION_NOT_SUPPORTED` **[P2]**.

---

## 9. الصفر

- `"0"`، `"0.00"`، `"000"`، `".0"` كلها `N = 0` و`S = 0`، والناتج «صفر» + المفرد **[R6]**.
- بدون وحدة: «صفر».

---

## 10. بدون وحدة (`none`)

- الجنس مذكر، ولا تمييز ولا إضافة (القسم 6، البند 3).
- `1` ← «واحد»، `2` ← «اثنان».
- الكسر: نص `N` + «فاصلة» + أرقام الكسر بعد التنقية مقروءة عدداً صحيحاً، وكل صفر بادئ فيها يُقرأ «صفر» **[R11]**. مثال افتراضي: `12.05` ← «اثنا عشر فاصلة صفر خمسة».

---

## 11. البادئة واللاحقة

`النتيجة = [prefix, النص, suffix]` بعد حذف الفارغ، مربوطة بمسافة واحدة: «فقط ألفان وخمسمائة ريال سعودي لا غير». البادئة واللاحقة نص حر لا يُعالج لغوياً.

---

## 12. الخيارات وواجهة البرمجة

```ts
tafqit(input: string, options?: {
  unit?: string;                 // معرّف الوحدة في units.json، الافتراضي "none"
  hundredsForm?: "مائة" | "مئة";  // الافتراضي "مائة"
  prefix?: string;               // الافتراضي ""
  suffix?: string;               // الافتراضي ""
  decimals?: "round" | "error";  // الافتراضي "round"
  language?: "ar" | "en" | "fr"; // الافتراضي "ar" (القسم 17)
}): string

// طبقة توافق بأسماء الحقول المرصودة، تعمل محلياً بلا خادم
convert({ the_number, unit, hundreds_form, language }): { result }

// قائمة اللغات لبناء منتقي اللغة في الواجهة (القسم 17.0)
LANGUAGES: readonly { id, label, dir: "rtl" | "ltr", supportsHundredsForm }[]
```

يقبل `unit` المعرّف (`"SAR"`) أو أي اسم مذكور في `aliases` (`"ريال سعودي"`)، ليصلح للاستدعاء من Excel.

---

## 13. الأخطاء

| الرمز | السبب |
|---|---|
| `EMPTY` | مدخل فارغ بعد حذف المسافات |
| `INVALID_FORMAT` | محارف غير مسموحة أو أكثر من فاصلة عشرية |
| `NEGATIVE` | عدد سالب **[P4]** |
| `TOO_LARGE` | الجزء الصحيح أكبر من الحد الأقصى |
| `TOO_MANY_DECIMALS` | خانات الكسر أكثر من `d` مع `decimals: "error"` **[P1]** |
| `FRACTION_NOT_SUPPORTED` | كسر مع وحدة بلا جزء فرعي **[P2]** |
| `UNKNOWN_UNIT` | معرّف أو اسم وحدة غير موجود |
| `INVALID_OPTION` | قيمة غير صالحة لـ `hundredsForm` أو `decimals` أو `language` |
| `INVALID_CATALOG` | ملف وحدات لا يطابق المخطط |
| `NO_LANGUAGE_FORMS` | لا توجد صيغ للوحدة (أو لجزئها الفرعي عند الحاجة إليه) باللغة المطلوبة (القسم 17.3) |

تُعيد النواة رمز الخطأ ورسالة عربية. وفي التفقيط المتعدد يظهر الخطأ في سطره ولا يوقف بقية الأسطر.

---

## 14. مخطط بيانات الوحدات

المخطط في [`data/units.schema.json`](../data/units.schema.json)، والبيانات في [`data/units.json`](../data/units.json). لكل وحدة:

| الحقل | إلزامي | الوصف |
|---|---|---|
| `id` | نعم | معرّف ثابت (`SAR`، `riyal`، `kilogram`) |
| `category` | نعم | فئة العرض في القائمة |
| `label` | نعم | الاسم المعروض |
| `code` | لا | رمز ISO 4217 أو رمز التداول |
| `aliases` | لا | أسماء بديلة للبحث (VBA والتوافق) |
| `forms` | نعم | `{singular, dual, plural, accusative, gender}`، أو `null` في «بدون وحدة» |
| `forms.one` | لا | عبارة العدد 1 عند الحاجة لتغيير الموضع: «كيلومتر واحد في الساعة» |
| `subunit` | لا | صيغ الجزء الفرعي نفسها + `ratio` (و`one` و`i18n` اختياريان) |
| `i18n` | لا | صيغ الوحدة باللغات الأخرى: `{ en?: {singular, plural, one?}, fr?: {singular, plural, gender, one?} }` (القسم 17.3). ممنوع مع `forms: null` |
| `note` | لا | ملاحظة للمراجعين، لا تؤثر على الناتج |

- `plural` هي الصيغة المستعملة مع 3–10، و`accusative` هي المستعملة مع 11–99.
- إضافة وحدة = إضافة كائن إلى `units.json` فقط. النواة تتحقق عند التحميل من مطابقة المخطط ومن تفرّد `id` و`aliases`.
- الفهرس الحالي: 106 وحدات (41 عملة عربية وعالمية، و3 عملات رقمية، و10 أسماء عامة، ووحدات الوزن والحجم والطول والمساحة والزمن والعدّ والبيانات والسرعة، و«بدون وحدة»).
- إضافة إلى العملات الخمس المذكورة في الطلب، يقسم الدينار الليبي إلى 1000 درهم والدينار التونسي إلى 1000 مليم (حقيقة نقدية لا قاعدة لغوية). الأوقية الموريتانية تقسم إلى 5 خمس فلا جزء فرعي لها.

---

## 15. الحالات الذهبية والتوافق مع VBA

- الحالات في [`tests/golden/cases.json`](../tests/golden/cases.json)، ولكل حالة: `id`، و`input`، و`unit`، و`options` الاختيارية (ومنها `language`)، و`cleaned` (الرقم بعد التنقية)، و`expected`، و`review` (رموز البنود المفتوحة التي تمسّها).
- اختبارات النواة تقرأ هذا الملف مباشرة.
- وحدة VBA تُولَّد جداول وحداتها من `units.json` بسكربت بناء، ويُولَّد منه أيضاً ماكرو اختبار من `cases.json` يقارن الناتج حرفاً بحرف. وتستخدم VBA النصوص و`Decimal`/`String` لا `Double`.
- أي تعديل لقاعدة يبدأ بتعديل `cases.json` ثم النواة ثم VBA.

---

## 16. القرارات اللغوية

حُسمت البنود التالية **حسب الشائع في أقسام المالية والحسابات** (الصكوك والفواتير وأنظمة المحاسبة)، لا حسب الوجه الأفصح دائماً. ويمكن مراجعتها لاحقاً بتعديل الحالات الذهبية أولاً (انظر CONTRIBUTING.md).

| الرمز | المسألة | القرار | الوجه الآخر (غير معتمد) | الحالات |
|---|---|---|---|---|
| **R1** | عدد مركب آخره 1 أو 2 | «مائة وواحد ريال»، «مائة واثنان ريال» | «مائة ريال وريال»، «مائة وريالان» | G13 |
| **R2** | المثنى قبل اسم الوحدة | «مائتان ريال»، «ألفان ريال»، «مليونان ريال»؛ و«مائتا ألف» قبل لفظ المرتبة | «مائتا ريال»، «ألفا ريال» (الأفصح) | G14، G16، G30 |
| **R3** | لفظ المرتبة المنصوب قبل الوحدة | «أحد عشر ألف ريال» | «أحد عشر ألفاً ريال» | G18 |
| **R4** | الواحد المؤنث في المعطوف | «إحدى وعشرون هللة» | «واحد وعشرون هللة» | G27 |
| **R5** | الثمانية مع المؤنث | «ثماني ليرات»، «ثماني عشرة»، «ثماني وعشرون» | «ثمانٍ وعشرون» | G24 |
| **R6** | الصفر | «صفر ريال» | «لا شيء» | G01 |
| **R7** | مرتبة آخرها 1 أو 2 بعد مئات | «مائة وواحد ألف» | «مائة ألف وألف» | — |
| **R8** | جمع فلس | «فلوس» | «أفلس» | — |
| **R9** | الأسماء الأجنبية | تُجمع بالألف والتاء (بيتكوينات، بايتات)؛ و«يورو» ثابت | الثبات في الكل | — |
| **R10** | موضع «واحد» | «ريال سعودي واحد»، وحقل `one` للوحدات المركبة | «ريال واحد سعودي» | — |
| **R11** | الكسر بلا وحدة | «اثنا عشر فاصلة صفر خمسة» | «فاصل» | — |
| **R12** | تنوين النصب | «ريالاً» (ألف + فتحتان) | «ريالا» | — |
| **R13** | جزء الدينار الأردني | 1000 فلس | 100 قرش | — |
| **R14** | جموع الوحدات الإقليمية | مدوَّنة في `note` لكل وحدة | — | — |
| **R15** | جنس «قدم» | مؤنث (ثلاث أقدام) | مذكر | — |
| **R16** | «ثوانٍ» بلا تشكيل | «ثلاث ثوان» | «ثلاث ثواني» | — |

### قرارات التشغيل

| الرمز | المسألة | القرار |
|---|---|---|
| **P1** | خانات كسر أكثر من دقة العملة | **تقريب نصفي للأعلى** بحساب نصي (الافتراضي)، فهو المعمول به محاسبياً ويعالج قيم Excel المحسوبة مثل `0.1+0.2`؛ والخيار `decimals: "error"` لمن يريد الرفض (G41، G42) |
| **P2** | كسر مع وحدة بلا جزء فرعي | خطأ `FRACTION_NOT_SUPPORTED`، لأن التقريب هنا يُسقط كمية فعلية (2.5 كرتونة) |
| **P3** | الفاصلة `,` | فاصل آلاف دائماً |
| **P4** | الأعداد السالبة | خطأ |

---

## 17. اللغات الأخرى / Other languages

يختار الخيار `language` لغة الناتج: `"ar"` (الافتراضي، الأقسام 0–16 بلا أي تغيير)، و`"en"`، و`"fr"`. وأي قيمة أخرى خطأ `INVALID_OPTION` (والنص الفارغ يعني الافتراضي، كما في `hundredsForm`). رسائل الأخطاء عربية في كل اللغات.

The `language` option selects the output language. Everything that is not words is shared by all languages and behaves exactly as in Arabic: input cleaning (§1), the 15-digit limit, fraction → subunit conversion and half-up rounding (§8, P1), `decimals: "error"`, `FRACTION_NOT_SUPPORTED` (P2), unit lookup (§12), prefix/suffix (§11) and the composition table of §8 (subunit omitted when `S = 0`, subunit alone when `N = 0`). `hundredsForm` is still validated but has no effect outside Arabic (`supportsHundredsForm: false`).

### 17.0 Architecture

- `src/engine.ts` — shared pipeline (§8–§10): resolves `N` and `S`, rounds, raises the shared errors, then asks the language module for words.
- `src/lang/<id>.ts` — one module per language behind the `LanguageModule` interface (`src/lang/types.ts`): `pickForms` (where the unit's forms live), `plain(N, fraction)` (no unit), `count(N, forms)` (number + noun) and `subunitJoiner`. Arabic is `src/lang/ar.ts` (the §3–§10 rules, unchanged; its word tables stay in `src/lexicon.ts`).
- `src/lang/index.ts` — the registry and `LANGUAGES`, exported from `src/index.ts`:

| `id` | `label` | `dir` | `supportsHundredsForm` |
|---|---|---|---|
| `ar` | العربية | `rtl` | `true` |
| `en` | English | `ltr` | `false` |
| `fr` | Français | `ltr` | `false` |

Adding a language = a new module + its entry in the registry, in `Language`/`UnitI18n` (`src/types.ts`), in `$defs/i18n` of the schema, and its forms in `units.json`.

### 17.1 English (`en`)

| Rule | Output |
|---|---|
| Case | Title Case for numbers and units: `One Thousand Two Hundred Fifty Saudi Riyals` |
| 0–19 | Zero, One, Two … Ten, Eleven … Nineteen |
| Tens | Twenty, Thirty, Forty, Fifty, Sixty, Seventy, Eighty, Ninety; compounds hyphenated: `Twenty-One`, `Ninety-Nine` |
| Hundreds | `One Hundred`, `Two Hundred Five` — no internal "and" **[EN1]** |
| Scales | Short scale, singular after a number: Thousand (10³), Million (10⁶), Billion (10⁹), Trillion (10¹²): `Two Million`, `Twenty-One Thousand` |
| Joining | Non-zero groups from high to low, single spaces; zero groups omitted: `One Million One Thousand Five` |
| Noun | `N = 1` → `one` if present, else `"One " + singular`; any other `N`, including 0, → words + `" " + plural`: `Zero Saudi Riyals` **[EN2]**, `Two Pence` |
| Subunit | integer phrase + `" and "` + subunit phrase: `Ten Moroccan Dirhams and Fifty Centimes` |
| No unit | integer words, then `Point` and each fraction digit read alone: `Twelve Point Zero Five`, `Three Point One Two Five` **[EN3]** |

### 17.2 French (`fr`)

Traditional orthography (pre-1990) as usually written on cheques and invoices, all lowercase **[FR1]**.

| Rule | Output |
|---|---|
| 0–16 | zéro, un/une, deux, trois, quatre, cinq, six, sept, huit, neuf, dix, onze, douze, treize, quatorze, quinze, seize |
| 17–19 | dix-sept, dix-huit, dix-neuf |
| Tens | vingt, trente, quarante, cinquante, soixante; 70–79 = soixante-dix…soixante-dix-neuf; 80 = quatre-vingts; 90–99 = quatre-vingt-dix…quatre-vingt-dix-neuf |
| Hyphens | between tens and units below 100: `dix-sept`, `quarante-deux`, `quatre-vingt-un`, `quatre-vingt-onze`; never next to `et`, `cent`, `mille`, `million`… |
| et | only in 21, 31, 41, 51, 61 (`vingt et un`) and 71 (`soixante et onze`); not in 81, 91, 101, 1001 (`quatre-vingt-un`, `cent un`, `mille un`) |
| un / une | agrees with the gender of the noun in the **last** 3-digit group only: `vingt et une livres`, `deux cent une livres`, `mille une livres`; higher groups are masculine: `vingt et un mille livres` **[FR3]**, `vingt et un millions de livres` |
| quatre-vingts | takes `s` only when final, i.e. when no other number word follows (a noun, million/milliard/billion or the end may follow): `quatre-vingts dirhams`, `quatre-vingts millions`; but `quatre-vingt-un`, `quatre-vingt mille` **[FR2]** |
| cent | `cent` (never "un cent") for 100; multiplied `cent` takes `s` when final in the same sense: `deux cents`, `deux cents dirhams`, `trois cents millions`; but `deux cent un`, `deux cent mille` **[FR2]** |
| mille | invariable, never preceded by "un": `mille`, `deux mille`, `deux cent mille` |
| Scales | long scale; million (10⁶), milliard (10⁹), billion (10¹²) are nouns: `un million`, `deux millions`, `deux cents milliards` **[FR8]** |
| Joining | groups from high to low with single spaces; zero groups omitted: `un million mille cinq` |
| Noun | singular for 0 and 1, plural from 2: `zéro dirham` **[FR4]**, `un dirham`, `deux dirhams`; `N = 1` uses `one` if present, else `un`/`une` + singular |
| de / d' | when the integer ends exactly with million/milliard/billion (`N ≥ 10⁶` and `N mod 10⁶ = 0`) the noun is preceded by `de`, or `d'` before a vowel or h: `un million de dirhams`, `deux milliards d'euros`, `deux cents millions d'heures` **[FR5]**; otherwise not: `un million cinq cents dirhams`, `un million mille dirhams`. The rule applies to the subunit as well (`0.01 BTC` → `un million de satoshis`) **[FR6]** |
| Subunit | integer phrase + `" et "` + subunit phrase: `mille deux cent cinquante dirhams marocains et soixante-quinze centimes` |
| No unit | masculine; integer words + `virgule` + each leading zero as `zéro`, then the rest as a whole number: `douze virgule zéro cinq`, `trois virgule cent vingt-cinq` **[FR7]** |

### 17.3 Unit forms (`i18n`)

```jsonc
"i18n": {
  "en": { "singular": "Saudi Riyal", "plural": "Saudi Riyals" },              // one? optional
  "fr": { "singular": "riyal saoudien", "plural": "riyals saoudiens", "gender": "m" } // one? optional
}
```

- On a unit (next to `forms`) and, separately, inside `subunit`. Not allowed on `none` (`forms: null`), which works in every language without forms.
- English: `singular` (with 1), `plural` (with every other number). French: `singular` (0 and 1), `plural` (2+), `gender` of the French noun (it can differ from the Arabic one: هللة is feminine, `halala` masculine). `one` replaces the whole phrase for exactly 1 in both.
- Strings are trimmed and non-empty; unknown keys or languages are rejected (`INVALID_CATALOG`). The engine never derives forms.
- Missing forms for the requested language → `NO_LANGUAGE_FORMS` («لا تتوفر صيغة لهذه الوحدة باللغة المختارة.»). The unit's forms are always required; the subunit's only when a non-zero subunit amount is written (`2.00` works, `2.50` fails).
- The bundled catalog has `en` and `fr` forms for all 105 units that have forms and for all 32 subunits. The Arabic content of `units.json` is unchanged.

### 17.4 Review items

Choices made without a native-speaker review. As in §16, a change starts with the golden cases (G43–G69 English, G70–G100 French).

| Code | Question | Current choice | Alternative | Cases |
|---|---|---|---|---|
| **EN1** | "and" inside numbers | none (US cheque style): `Two Hundred One` | British `Two Hundred and One` | G53 |
| **EN2** | Noun with 0 | plural: `Zero Saudi Riyals` | — | G43 |
| **EN3** | No-unit decimals | digit by digit: `Three Point One Two Five` (differs from ar/fr, which read the rest as a number) | `Three Point One Hundred Twenty-Five` | G67 |
| **EN4** | Spelling and names | **British spelling** (Gulf/Levant banking usage, consistent with `Piastre`): `Metre`, `Litre`, `Kilometre`, `Tonne`; Title Case keeps `per` lowercase (`Kilometre per Hour`); invariable plurals `Fils`, `Baisa`, `Yen`, `Yuan`, `Ringgit`, `Won`, `Lira` (generic and Turkish), `Kuruş`; `Paisa/Paise`, `Kronor`, `Kroner`, `Penny/Pence`; `UAE Dirham`; `Rial` (OMR/YER/IRR) vs `Riyal` (SAR/QAR) | US spelling | G68, G105–G107 |
| **EN5** | Hyphenated compounds in Title Case | `Twenty-One` | `Twenty-one` | G46 |
| **FR1** | Orthography | traditional: hyphens only below 100 and never with `et` | 1990 reform: hyphens everywhere (`deux-cent-vingt-et-un`, `mille-deux-cent-cinquante`) | all French |
| **FR2** | `cents` / `quatre-vingts` before million/milliard/billion | keep the `s` (they are nouns): `deux cents millions`, `quatre-vingts millions` | — (before `mille` no `s`: `deux cent mille`) | G97 |
| **FR3** | Feminine `une` before `mille` | masculine: `vingt et un mille livres` | `vingt et une mille livres` (also accepted) | G94 |
| **FR4** | Noun with 0 | singular: `zéro dirham` | `zéro dirhams` | G70 |
| **FR5** | `d'` before h | every h is treated as mute (`d'heures`, `d'hectares`); no unit with an aspirated h exists yet | per-unit flag if one is added | G85 |
| **FR6** | `de` after an exact million in the integer when a subunit follows | kept: `un million d'euros et cinquante centimes` | — | G85, G99 |
| **FR7** | No-unit decimals | `zéro` per leading zero, rest as a whole number: `douze virgule zéro cinq` | digit by digit | G96 |
| **FR8** | 10¹² | `billion` (long scale, French usage) | `mille milliards` | — |
| **FR9** | Unit names | *(after independent review: MRU is masculine `ouguiya mauritanien`; QAR `riyal qatarien`; generic lira `livre`, not the Italian `lire`; dollar `cent` kept — homograph of the numeral, see G104)* EUR subunit `centime` (usage in France; official `cent`); dollar subunit `cent/cents` (homograph of the numeral); SAR subunit `halala` masculine; `rial` for OMR/YER/IRR, `riyal` for SAR/QAR; `baïsa`, `paisa/paisas`, `kuruş` (invariable), `ether`, `tether`, `dounam`; `dirham émirati`; `livres sterling` (sterling invariable); `penny/pence`; year `an/ans` (not `année`); حبة `unité`, علبة `paquet`, صندوق `boîte`, `dollar de Singapour` | per-country usage | G88, G93, G99 |
| **FR10** | Capitalisation | lowercase; a prefix/suffix is free text, so `Arrêtée la présente facture à la somme de` is up to the caller | sentence case | G100 |
