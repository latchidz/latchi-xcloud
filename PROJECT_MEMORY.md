# PROJECT MEMORY — LATCHI xCLOUD

ذاكرة المشروع للصيانة المستقبلية. تُحدَّث مع كل إصدار.

## Project

**LATCHI xCLOUD** — مشغّل ويندوز مخصص لـ Xbox Cloud Gaming مع تكامل Better xCloud الرسمي. v0.1.0.

## Purpose

بيئة تشغيل خفيفة حول الخدمة الشرعية (نحن عميل فقط — لا نتحايل على مصادقة/اشتراكات/DRM إطلاقاً)، مُحسَّنة لحاسوب 4GB RAM / رسوميات Intel مدمجة / HDD / Windows 10-11 x64.

## Architecture (والسبب)

| القرار | السبب |
|---|---|
| **WPF (.NET 8) + WebView2 Evergreen** بدل Electron/CEFSharp | WebView2 = نفس Chromium المثبت مع الويندوز (يدعمه BxC رسمياً): WebRTC/WebGL/فك ترميز عتادي/Gamepad أصلية، مضيف WPF خفيف (~60-80MB بدل 250MB+ لElectron)، إقلاع سريع من HDD، وصيانة طويلة الأمد بلا تتبع إصدارات Chromium يدوياً. عقدة ضد: Electron ثقيل جداً للهدف؛ CEFSharp عبء ثنائيات وصيانة |
| مثيل WebView2 **واحد** طوال عمر التطبيق | حفظ الجلسة/الإعدادات/الحالة؛ بلا إعادة إنشاء عند كل تنقل (§52) |
| ملف تعريف مستقل `%LOCALAPPDATA%\LATCHI\xCLOUD\BrowserProfile` | عزل تام عن بيانات Chrome/Edge للمستخدم (§14) |
| شريط عنوان مخصص (WindowChrome) | مظهر تطبيق حقيقي، بلا واجهة متصفح (§10) |
| نشر self-contained + ReadyToRun **مجلد** | لا يتطلب تثبيت .NET لدى المستخدم؛ مجلد أسرع من single-file على HDD |
| مثبّت Inno `PrivilegesRequired=lowest` | بلا UAC؛ بيانات %LOCALAPPDATA% لا تُمس عند uninstall (لا [UninstallDelete]) |

## Browser Runtime

WebView2 SDK **1.0.4258.31**؛ Runtime = Evergreen المثبت مع ويندوز (يُفحص وجوده في CI للفحص الذاتي ويُثبَّت من رابط Microsoft الرسمي إن غاب). إصدار Runtime الفعلي يظهر في التشخيصات (`BrowserVersionString`).

## Better xCloud

- **المستودع الرسمي:** github.com/redphx/better-xcloud (MIT) — المصدر الوحيد المسموح
- **النسخة المدمجة:** v6.7.12 — الالتزام `f8397043f6d2148d2345d508902a38c69cf1ee20` (من tag الريليز الرسمي)
- **SHA-256:** `bb78931b4ec94d68cb18da97fa4ae1f88e6b3103bc159905931b2238a51b517e` (مطابق بايتاً لبايت، بلا أي تعديل)
- **طريقة التكامل:** السكربت مضمّن في `resources/` → يُنسخ لactive عند أول تشغيل → يُحقن عبر `AddScriptToExecuteOnDocumentCreatedAsync` (= document-start) بعد **جسر خفيف + حرس @match/@exclude مولّد من ميتاداتا السكربت نفسه** (كل قاعدة match تتحول لregex JS — المصدر الوحيد للحقيقة هو ترويسة السكربت الفعلي). v6.7.12 معلنة `@grant none` → **لا حاجة لأي GM APIs** (تحقق فحص+اختبار). إذا أصدرت نسخة مستقبلية تطلب GM_*, **يرفض المحدّث التحديث** (فشل آمن، لا تزييف)
- **كشف التهيئة:** `window.BX_EXPOSED` (تعيين صريح داخل السكربت عند التحميل) — فحص أحادي بعد كل تنقل (1.2s ثم 3.5s) بلا حلقات poll أثناء اللعب
- **إشارة فشل:** رسالة `bxc-error` من try/catch الحقن

## Authentication

لا نلمس كلمات السر ولا الرموز إطلاقاً: تسجيل الدخول يجري في صفحات Microsoft الرسمية داخل WebView2. الجلسة تُحفظ في ملف تعريف المتصفح المخصص (cookies/التخزين يديرهما WebView2 نفسه بشكل آمن).

## Session Storage

`BrowserProfile\` (يديره WebView2). بعد الإغلاق/إعادة الفتح: المستخدم يبقى مسجلاً ما دامت جلسة Microsoft صالحة (لا نعِد بدوام تسجيل دائم — قد تُبطلها Microsoft).

## Security

- **سياسة التنقل** (`NavigationPolicy`): قائمة لواحق نطاقات Microsoft/Xbox فقط (xbox.com, xboxlive.com, live.com, microsoft.com, microsoftonline.com, msauth.net, msftauth.net, msftauthimages.net, passport.net, azureedge.net) + عن بُعد حسب اللاحقة (dot-anchored — `evil-notxbox.com` مرفوض). أي شيء آخر: NavigationStarting يُلغى
- **النوافذ المنبثقة:** NewWindowRequested → دائماً Handled؛ المسموح يتحول لتنقل في نفس النافذة (المصادقة تبقى بالجلسة)، غير المسموح يُحظر + سجل
- **التنزيلات:** DownloadStarting → إلغاء دائماً
- **الصلاحيات:** رفض افتراضي؛ الميكروفون مسموح لxbox.com فقط (دردشة صوتية)
- **لا Host Objects** مكشوفة للصفحة — الجسر رسائل JSON فقط (postMessage→WebMessageReceived بتحقق صارم للأنواع)
- DevTools معطلة؛ صفحات أخطاء Chromium معطلة (شاشة خطأ التطبيق تحل محلها)
- قوائم السياق وCtrl+wheel zoom **يبقيان** — Paste في حقول تسجيل الدخول أهم من الشكل

## Controller

بلا أي اعتراض: Gamepad API داخل Chromium يعمل مباشرة من صفحة xCloud (قرص واحد من المسؤولية). التشخيصات تعرض عدد الأيدي المتصلة عبر `navigator.getGamepads()`. اختبار زمن الاستجابة الفعلي يتطلب جهازاً حقيقياً — غير ممكن في CI (NOT TESTED بصدق).

## Performance

- مضيف WPF خفيف؛ صفر مؤقتات دائمة (فحص أحادي بعد التنقل + مؤقت toast فقط عند الظهور)
- تسريع العتاد **لا يُمس** (لا additional-browser-arguments تعطّل GPU)
- وضع الجهاز الضعيف = تبسيط واجهة المضيف فقط
- القياسات الفعلية (RAM/CPU): NOT MEASURED — لا اختراع أرقام

## Build

GitHub Actions `build.yml`: build → **55 xUnit** → publish (win-x64 self-contained R2R مجلد) → **فحص smoke على exe الإنتاجي** (WebView2 حقيقي + حقن + حجب + جسر) → Inno Setup → بورتبل zip → manifest بصمات → artifact. ⛔ لا Release عام (المواصفة §66) — الأرتيفاكت عبر Actions فقط.

## ⛔ درس حرج: أبداً PushFrame متداخلة مع WebView2 (5 أكتوبر 2026)

أول تشغيل CI علّق 27 دقيقة: فحص الـsmoke كان يضخ **إطارات dispatcher متداخلة** (PushFrame) من داخل OnStartup بينما تكملات WebView2 غير المتزامنة تتنافس على نفس الـdispatcher → جمود دائم + `Start-Process -Wait` بلا سقف. الحل النهائي (لا تعد عنه):
1. الـsmoke يعمل كتدفق async واحد على **دورة رسائل التطبيق الحقيقية** (OnStartup يوزّع RunAsync ويعود؛ Application.Run يضخ طبيعياً).
2. **watchdog داخلي** يقتل العملية بعد 8 دقائق مهما حدث (Environment.Exit(1)).
3. الـworkflow: `Wait-Process -Timeout 600` ثم kill — لا انتظار غير محدود أبداً.

## Known Issues

1. التفاعلات الحقيقية (تسجيل دخول Microsoft، إطلاق لعبة، streaming، يد تحكم فعلية، صوت) **غير قابلة للاختبار في CI** — تم التحقق برمجياً مما يمكن، والباقي NOT TESTED بصدق في مصفوفة الاختبار
2. فحص WebGL/GPU في التشخيصات يعمل على المستند الحالي — على شاشة الخطأ (لا مستند حي) قد يظهر غير متاح
3. إعدادات BxC على نطاق xbox.com فقط في localStorage — «مسح بيانات BxC» يتطلب أن تكون الصفحة الحالية على xbox.com (يفحص الكود ذلك ويخبرك)
4. أيقونة SmartScreen ستظهر أول تشغيل (لا توقيع رقمي — كقرار المشروع)

## Update System

- **تحديث BxC:** رسمي فقط (GitHub API releases/latest) → تحقق (اسم+إصدار+حجم+@match+grant-none+SHA) → staging → **تفعيل عند بدء التشغيل القادم فقط** → rollback تلقائي عند فشل التهيئة + قائمة نسخ سيئة (ملف مستقل يصمد أمام الـrollback)
- **تحديث التطبيق نفسه:** منفصل تماماً — يتطلب بناء/مثبتاً جديداً (لا مزج)

## Future Work

- خيار إخفاء شريط العنوان كلياً في الوضع العادي (شريط يظهر بالمرور فقط)
- تمرير إعدادات إقلاع محافظة (locale ثابت للغة الواجهة)
- تقرير تشخيصات قابل للتصدير كملف
- عند توفر تعريب رسمي لInno، مثبّت عربي
