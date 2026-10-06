# PROJECT MEMORY — LATCHI xCLOUD

ذاكرة المشروع للصيانة المستقبلية. تُحدَّث مع كل إصدار.

## Project

**LATCHI xCLOUD** — مشغّل ويندوز مخصص لـ Xbox Cloud Gaming مع تكامل Better xCloud الرسمي. v1.0.0.

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

- **سياسة التنقل** (`NavigationPolicy`): قائمة لواحق نطاقات Microsoft/Xbox فقط (xbox.com, xboxlive.com, live.com, microsoft.com, microsoftonline.com, microsoftonline-p.com, msauth.net, msftauth.net, msftauthimages.net, passport.net, azureedge.net) + عن بُعد حسب اللاحقة (dot-anchored — `evil-notxbox.com` مرفوض). أي شيء آخر: NavigationStarting يُلغى
- **النوافذ المنبثقة:** NewWindowRequested → دائماً Handled؛ المسموح يتحول لتنقل في نفس النافذة (المصادقة تبقى بالجلسة والرؤية مضمونة — v1.2)، غير المسموح يُحظر + سجل منقّح
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

GitHub Actions `build.yml`: build → **55 xUnit** → publish (win-x64 self-contained R2R مجلد) → **فحص smoke على exe الإنتاجي** (WebView2 حقيقي + حقن + حجب + جسر) → Inno Setup → **بورتبل exe واحد** (Single-File مضغوط بذاته، بلا R2R لصالح الحجم ~80MB؛ استخراج المكتبات الأصلية تلقائي عند أول تشغيل) → manifest بصمات → artifact. ⛔ لا Release عام (المواصفة §66) — الأرتيفاكت عبر Actions فقط.

## v1.0 (5 أكتوبر 2026) — إضافات المستخدم
- **شاشة بدء احترافية** (`Views/SplashWindow`): نافذة مستقلة بأنيميشن، «Xbox Cloud Gaming» إنجليزية، صورة ملف شخصي اختيارية + شعار LATCHI، **نغمة إقلاع مُولَّدة تركيبياً** (أصلية 100% — صوت Xbox الحقيقي محمي بحقوق النشر؛ `assets/startup-chime.wav` كمورد WPF مضمن). حد أدنى للعرض 2.4s ثم fade-out عند جاهزية الصفحة.
- **معالج أول تشغيل** (`FirstRunWindow` + `Core/FirstRunFlow`): اختيار لغة (AR/EN) ← تسجيل دخول Microsoft (التدفق الشرعي داخل الويبفيو؛ كشف النجاح عبر نمط `www.xbox.com/*/auth/msa?*loggedIn*` الرسمي نفسه الذي يراقبه BxC) ← دخول تلقائي بملء الشاشة. تخطٍّ متاح.
- **سؤال الخروج**: «أبقِ حسابي مفتوحاً؟» نعم/لا + «لا تسألني مجدداً» — «لا» يمسح **كوكيز الدخول فقط** (`WebViewHost.ClearLoginCookiesAsync`: xbox.com/login.live.com/login.microsoftonline.com/account.live.com) ولا يمس بيانات BxC. الافتراضي "ask" والخيار محفوظ في `KeepSessionOnExit`.
- **ملء الشاشة افتراضياً** (`StartFullscreen=true`) مع **رقاقة عائمة للخروج** تظهر عند لمس الماوس للحافة العليا (bridge يرسل `mouse-top` مقيّداً بمعدل 400ms) + F11/Esc يعملان دائماً.
- **صورة الملف الشخصي** من الإعدادات (`Core/ProfileImage`: profile-image.png/jpg في دليل البيانات) — تُعرض في شاشة البدء بلا إعادة بناء.
- ⚠️ **درس airspace حرج:** عنصر WebView2 في WPF هو `HuidHost` — عناصر WPF فوقه **لا تُرسم أبداً**. جميع الطبقات الطافية أصبحت نوافذ مستقلة (Splash/FirstRun/Error) أو `Popup` (toast + رقاقة الخروج) لكل منها HWND خاص. شاشات 0.1.0 الداخلية كانت غير مرئية عملياً فوق الصفحة — أُصلح جذرياً في 1.0.
- **ترجمة AR/EN** (`Theme/Loc`) لنصوص المعالج/الحوارات/التلميحات؛ الـsplash إنجليزي دائماً بطلب المستخدم.

## v1.0.1 (6 أكتوبر 2026) — إصلاح الإقلاع عند المستخدم
المستخدم جرّب 1.0.0: شاشة البدء علقت عند «Connecting…» والمثبّت لم يظهر شيئاً. الجذور:
1. **DispatcherPriority.Background يتضور جوعاً**: جدولة المعالج وكل DispatcherTimers كانت Background (أدنى من Render) — صفحة xbox.com تُبقي قائمة الرسم مشغولة باستمرار → المعالج ومؤقت إخفاء الشاشة لا يعملان أبداً → «عالقة». **القاعدة الدائمة: كل ما يخص تدفق الإقلاع = Normal على الأقل؛ DispatcherTimer الافتراضي Background!**
2. نسخة ثانية على نفس مجلد البيانات (مثبت بعد محمولة عالقة) تتصادم على قفل ملف تعريف WebView2 → **ميوتكس Local\LATCHI-xCLOUD-single-instance** (بلا اكتساب في وضع smoke).
القرارات (بطلب المستخدم الصريح): **حذف SplashWindow وصوت الإقلاع وProfileImage نهائياً** (إقلاع مباشر: نافذة مكبّرة بأزرار الويندوز، StartFullscreen=false افتراضياً، F11 اختياري)؛ **حارس تنقل 30 ث** (أول تنقل لا يكتمل → شاشة خطأ بRetry بدل تعليق صامت)؛ **S10 في smoke: تنقل حقيقي إلى xbox.com يجب أن يكتمل خلال 45 ث** (فحص التعليق المُبلَّغ عنه فعلياً). الاختبارات 69.

## v1.1.0 (6 أكتوبر 2026) — إصلاح انهيار الإقلاع + تدقيق تكوين أول تشغيل
**الانهيار الحرج عند المستخدم**: «Set property 'System.Windows.Controls.Border.BorderBrush' threw an exception» فور فتح 1.0.0 قبل شاشة اللغة.
- **الجذر**: `ColBorder` مُعرَّف `<Color>` في Dark.xaml واستُعمل `BorderBrush="{StaticResource ColBorder}"` في MainWindow.xaml (سطرا 27/95) وFirstRunWindow (14) وsetter أسلوب Card (118) وsetter آخر (173) — Color لا يُحوَّل لBrush → XamlParseException وقت التشغيل. البناء أخضر لأن StaticResource لا يُحل وقت الترجمة، وsmoke القديم كان يبني نافذته الخاصة فلم يمس XAML الإنتاج قط.
- **الإصلاح**: المواضع الستة → `BrushBorder` (الفرشاة المعررفة أصلاً)؛ **اختباران جديدان يمنعان الفئة**: XamlBrushTests (تحليل ثابت لكل XAML: لا Color في خاصية Brush + لا StaticResource مفقود) وS11 (تحليل فعلي لكل نوافذ الإنتاج في CI على ويندوز حقيقي).
- **تسجيل الأعطال (§2)**: CrashReport يسجل النوع/الرسالة/سلسلة Inner كاملة/StackTrace، وXamlParseException يضيف BaseUri+Line+Position؛ معالجات: DispatcherUnhandledException + AppDomain + UnobservedTask؛ لا يُسجَّل أي سر (استثناءات فقط).
- **حارس حلقة الانهيار**: crash-streak.count في دليل البيانات — يُصفَّر عند خروج نظيف؛ ≥3 متتالية ← عرض إعادة تعيين الإعدادات (الجلسة/بيانات BxC تبقى).
- **تدفق أول تشغيل v1.1 (بطلب صريح)**: اللغة ← **إعدادات البث** ← الدخول، **والموقع لا يُفتح قبل اكتمال الإعداد**. FirstRunFlow صار 4 خطوات (language→stream→signin→done).
- **جسر الإعدادات الفعلي (§8)**: LATCHI UI → BxcSettings (Core) → `localStorage["BetterXcloud"]` → BxC نفسه. القيم من السكربت الرسمي حرفياً: `stream.video.resolution` = auto/720p/1080p/1080p-hq؛ `stream.locale` = default + 29 لغة (بينها ar-SA)؛ **المنطقة `server.region` ديناميكية من الخدمة داخل الصفحة** (STATES.serverRegions — ليست على window، لا يمكن قراءتها من المضيف) ← onboarding/الإعدادات تعرض Auto + إحالة لقائمة BxC الحية؛ `getGlobalPref/setGlobalPref` داخل IIFE (ليست على window) لذا القناة الوحيدة الصادقة = localStorage مباشرة: seed مرة واحدة قبل أول تنقل (document-created + علم BetterXcloud.Latchi.Seeded) ثم قراءة/كتابة حية عبر ExecuteScriptAsync + reload. لغة واجهة BxC نفسها (`bx.locale`) لا تدعم العربية (SUPPORTED_LANGUAGES بلا ar) — لغة LATCHI مستقلة تماماً.
- **S12 جديد**: الجسر نهاية-إلى-نهاية في CI — seed ب1080p/en-US قبل تنقل حقيقي إلى xbox.com ثم قراءة localStorage داخل الصفحة والمطابقة.
- **الاختبارات 83** (كانت 69): +7 BxcSettings (المخطط مطابق حرفياً للسكربت) +6 XamlBrush + تحديث FirstRun. الإصدار 1.1.0.

## v1.2.0 (6 أكتوبر 2026) — إصلاح شاشة الدخول الفارغة + خادم BxC + مساعد VPN

**قضية المستخدم الحرجية**: بعد الضغط على «تسجيل الدخول» تظهر شاشة فارغة ولا يحدث شيء. التشخيص الهندسي (سجل منقّح + سلسلة أحداث):
- **السبب الأرجح (جغرافي)**: xCloud غير متاح من IP الجزائر بلا VPN — صفحة /play تُحمَّل «بنجاح» لكنها شبه فارغة (المستخدم نفسه ذكر حاجته لاتصال فرنسا). الحل ليس تقنياً بل **إخبار صريح**: كاشف DOM فارغ + رسالة توفّر جغرافي + مساعد VPN خارجي.
- **التحليل التقني للنافذة المنبثقة**: `NewWindowRequested` لم يكن يوجَّه إلى أي مكان مرئي (تم إلغاؤه/تجاهله) ← أي «فتح تسجيل الدخول في نافذة جديدة» يعني لا شيء على الشاشة. **القرار النهائي (بعد استبعاد AuthWindow نهائياً — انظر الدرس)**: `e.Handled=true` + popup المسموح → `core.Navigate(uri)` في نفس النافذة — المصادقة تبقى داخل الجلسة والرؤية مضمونة.
- **نافذة مصادقة مستقلة (AuthWindow) مستحيلة بأمان**: `e.NewWindow` يتطلب CoreWebView2 مهيّأ داخل المعالج المتزامن؛ انتظار التهيئة بشكل متزامن على UI thread = deadlock (continuation يُجدوَل على Dispatcher محجوز)؛ وWebView2 جاهز مسبقاً مخفي يخالف قاعدة «لا عمليات مساعدة غير ضرورية». **لا تُعاد المحاولة أبداً.**
- **microsoftonline-p.com** كان ناقصاً من allowlist (قائمة Microsoft الرسمية تشمل `*.aadcdn.microsoftonline-p.com` و`*.microsoftonline-p.com`) — أُضيف. سلسلة إعادة توجيه AAD كاملة الآن مغطاة بلا فتح التنقل.

**ما بُني في v1.2.0:**
1. **طبقة تحميل مرئية**: شعار LATCHI (BackPanel) يظهر قبل أول تنقل؛ عند NavigationStarted: البانل يختفي + WebView يظهر + `LoadingPopup` («جارٍ تحميل صفحة Microsoft…»، أيقونة سحابة تنبض DoubleAnimation) — يُخفى عند ContentLoaded أو بعد اكتمال ناجح +1.5ث. لا شاشة فارغة صامتة أبداً.
2. **كاشف الصفحة الفارغة**: بعد NavigationCompleted ناجح +6ث → `IsPageEmptyAsync()` (ExecuteScriptAsync: body.innerText.trim().length + childElementCount) → إن كانت فارغة: `emptyPageTitle/emptyPageDetail` (توفّر جغرافي + مساعد VPN) + Retry/Diagnostics.
3. **سجل تنقّل منقّح** (`NavigationLog`، 60 خط): start/content/done(ok/fail)/popup/blocked — التمثيل عبر `Sanitize()` (قَطع query strings، حد 160). يظهر في DiagnosticsWindow. **ConsoleMessageLogged غير موجود في WebView2** (خطأ شائع من CefSharp) — البديل: `ConsoleTapJs` document-created script يلتقط window.onerror/unhandledrejection → postMessage {type:'js-error'} → سجل فقط.
4. **الخادم/المنطقة (server.region)**: `RegionOption`/`ServerRegions` (20 = Auto + 19 منطقة PascalCase من `SERVER_EXTRA_INFO` في السكربت المدمج؛ France=WestEurope) + `NormalizeRegion` (مطابقة حرفية؛ قيمة خاطئة=default لا no-op صامت). seed/apply/read تنقل الخادم عبر نفس `localStorage["BetterXcloud"]`. الإعداد في onboarding + Settings + S12 يتحقق نهاية-إلى-نهاية بWestEurope.
5. **معالج أول تشغيل 6 خطوات** (§9): language→quality→server→gamelang→vpn→signin (StepStream حُذف). FirstRunWindow أُعيدت كتابته كلياً: CmbQuality/CmbServer/CmbGameLang من BxcSettings، خطوة VPN بكشف/فتح/حالة Planet VPN، خصائص SelectedLanguage/SelectedStreamQuality/SelectedServerRegion/SelectedGameLanguage/Result.
6. **VpnHelper (خدمة جديدة)**: كشف Planet VPN المثبت (ProgramFiles/LocalAppData + planet*.exe + اختصارات Start Menu) / TryOpenClient (Process.Start على المسار المكتشف) / GetConnectionStatus (فحص محلي واحد: NetworkInterface من نوع Ppp/Tunnel — اسم الواجهة فقط، **لا ادعاء بلد/فرنسا بلا تحقق، لا استعلام IP متكرر**). **تكامل خارجي صرف** — لا نسخ ملفات، لا تثبيت صامت، لا بروكسي، لا reverse engineering (لا CLI/API/deep-link موثق للعميل — تم التحقق من support.freevpnplanet.com).
7. **بطاقة VPN في Settings** + خادم في بطاقة البث + قراءة حية تشمل الخادم.
8. **إصلاح الماوس (طلب المستخدم بالدارجة)**: `WindowChrome.IsHitTestVisibleInChrome="True"` على ChipBxc وStackPanel شريط الأدوات — hover+click يعملان داخل منطقة العنوان.
9. **الاختبارات 89** (كانت 83): +ServerRegions (20/19/WestEurope/مفاتيح حرفية) +NormalizeRegion +RegionScript +خطوات 6 +microsoftonline-p.com +LocTests (كل Loc.S المستخدمة موجودة + مفاتيح v1.2). الإصدار 1.2.0 (csproj + iss fallback).
10. **لا سباق إطلاق لل WebView**: Web يبدأ Collapsed ويظهر عند أول NavigationStarted (GoHome يليه دائماً NavigationStarted الذي يكشفه).

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
