using System.Text;
using LatchiXcloud.Core.Userscript;

namespace LatchiXcloud.App.WebView;

/// <summary>
/// Builds the single script injected at document creation (the exact equivalent of a
/// Tampermonkey "document-start" injection). It has two parts:
///
///  1. Host bridge — tiny, runs on every document: hotkeys (F11 / Ctrl+Shift+R/U/B,
///     Esc-aware fullscreen), the Better xCloud probe, and the targeted data-clear helper.
///  2. Better xCloud — the OFFICIAL userscript, byte-identical, executed only when
///     location.href satisfies the @match/@exclude rules parsed from the script's own
///     metadata header. No GM APIs are needed: Better xCloud declares "@grant none"
///     (verified for v6.7.12) and runs directly in the page context, exactly like
///     Tampermonkey's grant-less page-context mode.
///
/// If a future Better xCloud release starts requiring GM_* APIs, the metadata check in
/// BetterXcloudUpdateService will refuse the update (host does not fake those APIs) —
/// that is deliberate: fail safe, never pretend.
/// </summary>
public static class LoaderJs
{
    public static string Build(UserscriptMetadata meta, string bxcSource)
    {
        var sb = new StringBuilder();
        sb.Append(Bridge);
        sb.Append(MatchGuardHeader(meta));
        sb.Append('\n');
        sb.Append(bxcSource); // official script, RAW — never modified, never escaped
        sb.Append('\n');
        sb.Append(MatchGuardFooter);
        return sb.ToString();
    }

    /// <summary>JS string literal for a regex source (escape backslash + quotes).</summary>
    private static string JsString(string s) =>
        "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n") + "\"";

    private static string MatchGuardHeader(UserscriptMetadata meta)
    {
        var matches = string.Join(",", meta.Matches.Select(m => JsString(PatternConverter.ToJsRegexSource(m))));
        var excludes = string.Join(",", meta.Excludes.Select(m => JsString(PatternConverter.ToJsRegexSource(m))));
        // plain concatenation: no raw-string brace escaping pitfalls
        return "(function () {\n" +
               "  var MATCHES = [" + matches + "];\n" +
               "  var EXCLUDES = [" + excludes + "];\n" +
               "  var test = function (list, url) {\n" +
               "    for (var i = 0; i < list.length; i++) { if (new RegExp(list[i]).test(url)) return true; }\n" +
               "    return false;\n" +
               "  };\n" +
               "  var href = location.href;\n" +
               "  if (test(MATCHES, href) && !test(EXCLUDES, href)) {\n" +
               "    try {\n";
    }

    private const string MatchGuardFooter =
"""
    } catch (err) {
      try { window.chrome.webview.postMessage({ type: 'bxc-error', error: String(err && err.message || err) }); } catch (e) {}
    }
  }
})();
""";

    private const string Bridge =
"""
(function () {
  if (window.__LATCHI__) return;
  window.__LATCHI__ = true;

  var post = function (msg) {
    // post the OBJECT: WebMessageAsJson on the host side then receives clean JSON
    try { window.chrome.webview.postMessage(msg); } catch (e) {}
  };

  // ── host-level hotkeys only (never ordinary game keys) ──────────────
  document.addEventListener('keydown', function (e) {
    if (e.key === 'F11') { post({ type: 'hotkey', key: 'F11' }); e.preventDefault(); return; }
    if (e.ctrlKey && e.shiftKey && !e.altKey) {
      var k = (e.key || '').toLowerCase();
      if (k === 'r') { post({ type: 'hotkey', key: 'CtrlShiftR' }); e.preventDefault(); return; }
      if (k === 'u') { post({ type: 'hotkey', key: 'CtrlShiftU' }); e.preventDefault(); return; }
      if (k === 'b') { post({ type: 'hotkey', key: 'CtrlShiftB' }); e.preventDefault(); return; }
    }
    if (e.key === 'Escape') {
      // the page handles its own fullscreen first; only when it is NOT the page's
      // fullscreen do we ask the host to leave host-fullscreen
      if (!document.fullscreenElement && !document.webkitFullscreenElement) {
        post({ type: 'hotkey', key: 'Esc' });
      }
    }
  }, true);

  // ── top-edge reporter: reveals the floating exit-fullscreen chip while in host fullscreen ──
  // (throttled — one message at most every 400 ms, nothing while the user just plays)
  var lastTop = 0;
  document.addEventListener('mousemove', function (e) {
    if (e.clientY < 6 && (Date.now() - lastTop) > 400) {
      lastTop = Date.now();
      post({ type: 'mouse-top' });
    }
  }, true);

  // ── Better xCloud state probe (called by the host — no polling loops) ──
  window.__LATCHI_PROBE = function () {
    return JSON.stringify({ bxc: !!window.BX_EXPOSED });
  };

  // ── targeted data clear: only Better xCloud keys, never the login session ──
  window.__LATCHI_CLEAR_BXC = function () {
    try {
      var removed = 0;
      for (var i = 0; i < localStorage.length; ) {
        var k = localStorage.key(i);
        if (k && k.indexOf('BetterXcloud') === 0) { localStorage.removeItem(k); removed++; }
        else i++;
      }
      if (indexedDB.databases) {
        indexedDB.databases().then(function (dbs) {
          (dbs || []).forEach(function (db) {
            if (db.name && db.name.indexOf('BetterXcloud') >= 0) { try { indexedDB.deleteDatabase(db.name); } catch (e) {} }
          });
        });
      }
      return JSON.stringify({ ok: true, removed: removed });
    } catch (err) { return JSON.stringify({ ok: false, error: String(err) }); }
  };
})();
""";
}
