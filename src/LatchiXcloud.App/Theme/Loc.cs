namespace LatchiXcloud.App.Theme;

/// <summary>
/// UI strings for the two supported locales: Arabic + English (spec: AR/EN only).
/// The splash screen itself is always English ("Xbox Cloud Gaming") by explicit design.
/// </summary>
public static class Loc
{
    private static readonly Dictionary<string, (string Ar, string En)> Table = new()
    {
        // first-run wizard
        ["wizardLangTitle"] = ("اختر لغتك المفضّلة", "Choose your preferred language"),
        ["wizardLangSub"]   = ("يمكنك تغييرها لاحقاً من الإعدادات", "You can change it later in Settings"),
        ["wizardSignInTitle"] = ("سجّل الدخول بحساب Microsoft", "Sign in with your Microsoft account"),
        ["wizardSignInDetail"] = (
            "افتح صفحة تسجيل الدخول وأدخل حسابك — الجلسة تبقى محفوظة داخل التطبيق، وفي المرة القادمة يدخل مباشرة إلى Xbox Cloud Gaming.",
            "Open the sign-in page and enter your account — the session stays saved inside the app, and next time it goes straight to Xbox Cloud Gaming."),
        ["wizardSignInOpen"] = ("تسجيل الدخول الآن", "Sign in now"),
        ["wizardSkip"] = ("تخطّي الآن", "Skip for now"),
        ["wizardStreamTitle"] = ("إعدادات البث", "Stream settings"),
        ["wizardQualityTitle"] = ("جودة البث", "Stream quality"),
        ["wizardQualityDetail"] = (
            "تُطبَّق على Better xCloud قبل فتح الموقع — تلقائي موصى به للأجهزة الضعيفة.",
            "Applied to Better xCloud before the site opens — Auto is best for low-end devices."),
        ["wizardServerTitle"] = ("الخادم / المنطقة", "Server / region"),
        ["wizardServerDetail"] = (
            "تلقائي يختار أقرب خادم. من فرنسا يمكنك اختيار «غرب أوروبا» لأفضل بنغ مع الجزائر.",
            "Auto picks the nearest server. From France you can pick West Europe for the best latency to Algeria."),
        ["wizardGameLangTitle"] = ("لغة الألعاب", "Game language"),
        ["wizardGameLangDetail"] = (
            "لغة محتوى الألعاب — مستقلة تماماً عن لغة واجهة التطبيق.",
            "The games' content language — completely independent from the app UI language."),
        ["wizardVpnTitle"] = ("مساعد الاتصال (اختياري)", "Connection helper (optional)"),
        ["wizardVpnDetail"] = (
            "Xbox Cloud Gaming غير متوفر في كل البلدان. إن احتجت اتصالاً من منطقة مدعومة (فرنسا مثلاً) شغّل Planet VPN من هنا ثم تحقق من الحالة. التطبيق يعمل بلا VPN وكل شيء واضح أمامك — لا توجيه خفي أبداً.",
            "Xbox Cloud Gaming is not available in every country. If you need a supported region (e.g. France), launch Planet VPN here and check the status. The app works without a VPN and nothing is ever routed secretly."),
        ["wizardVpnOpen"] = ("فتح Planet VPN", "Open Planet VPN"),
        ["wizardVpnCheck"] = ("فحص الحالة", "Check status"),
        ["vpnNotInstalled"] = (
            "لم يُعثر على Planet VPN مثبتاً على هذا الجهاز.\nثبّته من موقعه الرسمي freevpnplanet.com ثم ارجع لهنا — لا نقوم بأي تثبيت نيابة عنك.",
            "Planet VPN is not installed on this PC.\nInstall it from the official site freevpnplanet.com, then come back — we never install anything for you."),
        ["vpnStatusConnected"] = ("ال VPN: متصل", "VPN: connected"),
        ["vpnStatusInstalledNotConnected"] = (
            "ال VPN: غير متصل — Planet VPN مثبت، افتحه واتصل يدوياً ثم افحص الحالة.",
            "VPN: not connected — Planet VPN is installed; open it, connect manually, then check again."),
        ["vpnStatusNotInstalled"] = (
            "Planet VPN غير مثبت — يمكنك المتابعة بدون VPN.",
            "Planet VPN is not installed — you can continue without a VPN."),
        ["wizardStreamDetail"] = (
            "تُطبَّق على Better xCloud قبل فتح الموقع. يمكنك تغييرها لاحقاً من الإعدادات.",
            "Applied to Better xCloud before the site opens. You can change them later in Settings."),
        ["wizardQuality"] = ("جودة / دقة البث:", "Stream quality / resolution:"),
        ["wizardGameLang"] = ("لغة الألعاب (لغة المحتوى):", "Game language (content language):"),
        ["wizardGameLangNote"] = (
            "مستقلة عن لغة واجهة التطبيق — مثال: واجهة عربية وألعاب إنجليزية.",
            "Independent from the app UI language — e.g. Arabic UI + English games."),
        ["wizardRegionNote"] = (
            "الخادم/المنطقة: تلقائي — قائمة الخوادم المتاحة فعلياً تُعرض داخل إعدادات Better xCloud بواجهة xCloud لأنها تُجلب مباشرة من الخدمة.",
            "Server/region: Auto — the actual list of available servers is shown inside Better xCloud's settings in the xCloud UI, because it is fetched live from the service."),
        ["loginLoading"] = ("جارٍ تحميل صفحة Microsoft…", "Loading the Microsoft page…"),
        ["emptyPageTitle"] = ("الصفحة لا تعرض أي محتوى", "The page is showing no content"),
        ["emptyPageDetail"] = (
            "اكتمل التحميل لكن الصفحة فارغة. السبب الأكثر شيوعاً: Xbox Cloud Gaming غير متوفر من موقعك الجغرافي الحالي — جرّب الاتصال من منطقة مدعومة (فرنسا مثلاً عبر VPN) ثم أعد المحاولة، أو راجع التشخيصات.",
            "Loading finished but the page is empty. The most common cause: Xbox Cloud Gaming is not available from your current location — connect from a supported region (e.g. France via VPN) and retry, or check Diagnostics."),
        ["wizardNext"] = ("متابعة", "Continue"),
        ["wizardBack"] = ("رجوع", "Back"),
        ["welcomeSaved"] = ("تم تسجيل الدخول وحفظ جلستك — أهلاً بك!", "Signed in and session saved — welcome!"),

        // exit / keep-session dialog
        ["keepTitle"] = ("جلستك في Xbox Cloud Gaming", "Your Xbox Cloud Gaming session"),
        ["keepMessage"] = ("هل تريد أن يبقى حسابك مفتوحاً عند إغلاق التطبيق؟", "Keep your account signed in when the app closes?"),
        ["keepYes"] = ("نعم، أبقِ حسابي مفتوحاً", "Yes, keep me signed in"),
        ["keepNo"] = ("لا، سجّل خروجي", "No, sign me out"),
        ["keepRemember"] = ("لا تسألني مجدداً", "Don't ask me again"),

        // toolbar tooltips
        ["tipHome"] = ("الصفحة الرئيسية (xCloud)", "Home (xCloud)"),
        ["tipReload"] = ("إعادة تحميل (Ctrl+Shift+R)", "Reload (Ctrl+Shift+R)"),
        ["tipFullscreen"] = ("ملء الشاشة (F11)", "Fullscreen (F11)"),
        ["tipSettings"] = ("الإعدادات", "Settings"),
        ["tipDiag"] = ("التشخيصات", "Diagnostics"),
        ["tipExitFs"] = ("العودة إلى وضع النافذة (F11 / Esc)", "Back to windowed (F11 / Esc)"),
        ["tipMin"] = ("تصغير", "Minimize"),
        ["tipMax"] = ("تكبير", "Maximize"),
        ["tipClose"] = ("إغلاق", "Close"),

        // errors
        ["errOfflineTitle"] = ("لا يوجد اتصال بالإنترنت.", "You are offline."),
        ["errOfflineDetail"] = ("اضغط «إعادة المحاولة» بعد استعادة الاتصال.", "Press Retry once your connection is back."),
        ["errLoadTitle"] = ("Xbox Cloud Gaming could not be loaded.", "Xbox Cloud Gaming could not be loaded."),
        ["errWebviewTitle"] = ("تعذّر تشغيل محرك العرض (WebView2).", "Could not start the WebView2 engine."),
        ["errBlockedNav"] = ("تم حظر فتح موقع خارج نطاق Xbox/Microsoft", "Navigation outside Xbox/Microsoft was blocked"),

        // settings
        ["setOnExit"] = ("عند إغلاق التطبيق:", "When the app closes:"),
        ["setOnExitAsk"] = ("اسألني في كل مرة", "Ask me every time"),
        ["setOnExitKeep"] = ("أبقِ جلستي مفتوحة", "Keep me signed in"),
        ["setOnExitSignout"] = ("سجّل خروجي", "Sign me out"),

        // misc
        ["appLangNote"] = ("(تُطبَّق بعد إعادة التشغيل)", "(applies after restart)"),
    };

    public static string S(string lang, string key)
        => Table.TryGetValue(key, out var v)
            ? (lang == "en" ? v.En : v.Ar)
            : key;
}
