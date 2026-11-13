# أدوات التفقيط الأخرى · Competitors

<div dir="rtl">

جدول بأدوات أخرى تكتب المبالغ بالحروف بالعربية (مواقع، وإضافات Excel، وتطبيقات، ومكتبات). كل سطر له مصدر، وما لم نتحقق منه مكتوب «غير متحقق». المعلومات من صفحات الأدوات نفسها أو من منشورات أصحابها كما قرأناها في **2026-11-13**، ولم نجرّب أي أداة منها بأنفسنا، فلا نقول شيئاً عن دقة نتائجها. وتبقى سياسة «الغرفة النظيفة» سارية: لا ننسخ شيفرة أو نصوصاً أو بيانات من أي منها (انظر [`CONTRIBUTING.md`](../CONTRIBUTING.md)).

</div>

Other amount-in-words (tafqit) tools for Arabic. Every row cites its source; anything we could not confirm says "not verified". Facts are as stated by each tool's own page or its author's post, read on **2026-11-13**. We have not run any of these tools, so nothing here is a claim about their accuracy. Clean-room rule applies: copy no code, text or data from them.

| Tool | Kind | Output languages | Price | Offline / privacy | Excel | Source |
|---|---|---|---|---|---|---|
| **InstaWords** (this repo) | Website, Windows app, Excel add-in + VBA module, TS library | Arabic, English, French | Free (proprietary) | Site works offline after the first visit; the app is offline except the optional update check | `=TAFQIT()` in `InstaWords.xlam` (not yet tried in a real Excel) | this repository |
| Noqta «Tafqit» converter | Website | Arabic (other languages not mentioned) | Free, unlimited | Runs in the browser; the page says the number is not sent to a server. Offline mode not mentioned | No | [noqta.tn](https://noqta.tn/en/number-to-arabic-words) |
| Noqta Tafqit API | Web API (early access, "being built") | Arabic (languages not listed) | Free 1,000 calls/month; Pro $9/month; Business $29/month | Online API | `=TAFQIT()` for Excel and Google Sheets announced in the Pro plan; not verified as shipped | [noqta.tn/en/tafqit-api](https://noqta.tn/en/tafqit-api) |
| Tafqeet.com | Website + API (API key on sign-up) | Arabic, English, French | not verified | Online | Not mentioned | [dev.to post by the author](https://dev.to/qaaiss/effortlessly-convert-numbers-to-words-with-tafqeetcom-the-ultimate-multi-language-tool-1ck0) |
| Hijri-calendars.com Tafqeet | Website | Arabic, optional tashkeel; currency labels SAR, USD, EUR, GBP, AED, KWD | Not stated (site says its tools are free) | Page says it runs in the browser and sends nothing | No | [hijri-calendars.com](https://hijri-calendars.com/number-to-words-arabic.php?lang=en) |
| DITAFQEET (di.jo) | Excel VBA module `DI.bas` + sample workbook | Arabic | Not stated | Offline (VBA in the workbook) | `=DITAFQEET()`; workbook must be saved as macro-enabled | [blog post, 2014](https://mohdaoud.blogspot.com/2014/08/arabic-english-amount-in-words-for-excel.html) |
| «تفقيط الارقام» on Gumroad | Excel template (per search snippet: decimals up to 3 places, 10+ Arab currencies) | Arabic | Paid per search snippet; not verified (product page shows only a title) | not verified | not verified | [abushama.gumroad.com](https://abushama.gumroad.com/l/axvwl) |
| SpellNumberEDP (ExcelDataPro) | Free VBA tutorial code | **English only** (Riyals and Halalas) | Free tutorial | Offline | `=SpellNumberEDP()` | [exceldatapro.com](https://exceldatapro.com/spellnumber-saudi-riyals) |
| Tawfir.Tafqit | .NET library (NuGet), v1.1.2 of 2026-10-27 | Arabic, English, Persian, Turkish; «فقط … لا غير» option | Free, closed source | Offline (library) | No | [nuget.org](https://www.nuget.org/packages/Tawfir.Tafqit) |
| alkoumi/laravel-arabic-tafqeet | PHP/Laravel package, MIT | Arabic (SAR default; SAR, EGP, KWD named) | Free | Offline (library) | No | [packagist.org](https://packagist.org/packages/alkoumi/laravel-arabic-tafqeet) |
| Cheque-kit | iPhone/iPad app | Arabic listed among 18 languages (the store's own "Languages" field is inconsistent): not verified | Free | not verified | No | [App Store](https://apps.apple.com/lb/app/cheque-kit-help-write-cheques/id820320582) |
| Manager.io (accounting software) | Built-in "amount in words" on invoices | Arabic said to be added in version 22.9.1: not verified | not verified | not verified | No | [forum.manager.io](https://forum.manager.io/t/41949) |

Not found (searched 2026-11-13): an Arabic tafqit add-in on Microsoft Marketplace/AppSource (the listings we found, e.g. [WA200006811](https://marketplace.microsoft.com/en-sa/product/WA200006811), describe English only, and two others could not be opened: HTTP 403), and a specific Arabic tafqit app on Google Play (the search returned no app page). Both are "not verified", not "do not exist".

What only InstaWords combines, as far as this table shows: Arabic, English and French output in one tool; a desktop app, an offline website and an Excel add-in that give the same text (CI parity tests); free of charge. Limits to keep in mind: the Windows app and the Excel add-in are not yet tested on a real Windows PC or a real Excel.
