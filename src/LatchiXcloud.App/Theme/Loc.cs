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
        ["setProfilePic"] = ("صورة الملف الشخصي (تظهر في شاشة البدء)", "Profile picture (shown on the splash screen)"),
        ["setChoosePic"] = ("اختيار صورة…", "Choose picture…"),
        ["setRemovePic"] = ("إزالة", "Remove"),
        ["setPicHint"] = ("تظهر عند الإقلاع القادم. PNG أو JPG.", "Appears on next launch. PNG or JPG."),
        ["setStartupSound"] = ("صوت الإقلاع عند التشغيل", "Play the startup sound"),
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
