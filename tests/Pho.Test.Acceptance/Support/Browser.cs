using System.Globalization;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Jint;

namespace Pho.Test.Acceptance.Support;

/// <summary>
/// The few parts of a web browser that Pho's page scripts rely on — the colour-scheme preference,
/// per-site storage, the root element's attributes and the "leave this page?" prompt — emulated
/// well enough to run the page's real inline scripts (with Jint) and the real stylesheet cascade.
/// Storage survives from one visit to the next, as it does in a browser.
/// </summary>
public sealed class Browser(PhoApp app)
{
    private string _storageJson = "{}";

    public bool SystemPrefersDark { get; set; }

    public Visit? Current { get; private set; }

    /// <summary>Loads the admin UI's start page and runs its scripts, as a fresh visit.</summary>
    public async Task<Visit> Open()
    {
        var html = await app.Admin.GetStringAsync("/");
        Current = new Visit(this, html);

        return Current;
    }

    public Visit RequireVisit() => Current ?? throw new InvalidOperationException("The admin UI has not been opened in the browser.");

    public sealed class Visit
    {
        private readonly Browser _browser;
        private readonly Engine _engine = new();

        public IDocument Document { get; }

        internal Visit(Browser browser, string html)
        {
            _browser = browser;
            Document = new HtmlParser().ParseDocument(html);

            _engine.Execute($$"""
                var localStorage = {
                    _s: {{browser._storageJson}},
                    getItem: function (k) { return Object.prototype.hasOwnProperty.call(this._s, k) ? this._s[k] : null; },
                    setItem: function (k, v) { this._s[k] = String(v); },
                    removeItem: function (k) { delete this._s[k]; }
                };
                var document = {
                    documentElement: {
                        _a: {},
                        setAttribute: function (n, v) { this._a[n] = String(v); },
                        removeAttribute: function (n) { delete this._a[n]; },
                        getAttribute: function (n) { return Object.prototype.hasOwnProperty.call(this._a, n) ? this._a[n] : null; }
                    },
                    getElementById: function () { return null; }
                };
                var window = {
                    _l: {},
                    addEventListener: function (n, f) { (this._l[n] = this._l[n] || []).push(f); }
                };
                """);

            foreach (var script in Document.QuerySelectorAll("script").Where(s => !s.HasAttribute("src")))
                _engine.Execute(script.TextContent);

            Persist();
        }

        private void Persist() => _browser._storageJson = _engine.Evaluate("JSON.stringify(localStorage._s)").AsString();

        /// <summary>Clicks a button in the page header, running its inline handler.</summary>
        public void ClickButton(string text)
        {
            var button = Document.QuerySelectorAll("button").First(b => b.TextContent.Trim() == text);
            _engine.Execute(button.GetAttribute("onclick")!);
            Persist();
        }

        public void Call(string function, params object[] args)
        {
            _engine.Invoke(function, args);
            Persist();
        }

        /// <summary>Whether trying to close or reload the page would make the browser ask first.</summary>
        public bool WarnsBeforeLeaving()
            => _engine.Evaluate("""
                (function () {
                    var e = { prevented: false, returnValue: undefined, preventDefault: function () { this.prevented = true; } };
                    (window._l['beforeunload'] || []).forEach(function (f) { f(e); });
                    return e.prevented;
                })()
                """).AsBoolean();

        public string? ThemeAttribute
        {
            get
            {
                var value = _engine.Evaluate("document.documentElement.getAttribute('data-theme')");
                return value.IsNull() ? null : value.AsString();
            }
        }

        /// <summary>The page background the stylesheet resolves to, given the system preference and the root's theme.</summary>
        public string Background => ResolveCustomProperty("--bg");

        public bool LooksDark => Luminance(Background) < 0.5;

        private string ResolveCustomProperty(string property)
        {
            var css = string.Concat(Document.QuerySelectorAll("style").Select(s => s.TextContent));
            var theme = ThemeAttribute;
            string? winner = null;
            var winnerSpecificity = -1;

            foreach (var (media, selector, declarations) in Rules(css))
            {
                if (media is not null && !MediaApplies(media)) continue;
                if (!SelectorApplies(selector.Trim(), theme, out var specificity)) continue;

                var match = Regex.Match(declarations, Regex.Escape(property) + @"\s*:\s*([^;]+)");
                if (!match.Success || specificity < winnerSpecificity) continue;

                winner = match.Groups[1].Value.Trim();
                winnerSpecificity = specificity;
            }

            return winner ?? throw new InvalidOperationException($"The stylesheet never sets {property}.");
        }

        private bool MediaApplies(string media)
        {
            var m = Regex.Match(media, @"prefers-color-scheme:\s*(\w+)");
            if (!m.Success) return false;

            return m.Groups[1].Value == (_browser.SystemPrefersDark ? "dark" : "light");
        }

        private static bool SelectorApplies(string selector, string? theme, out int specificity)
        {
            specificity = 0;
            if (selector == ":root")
            {
                specificity = 1;
                return true;
            }

            var m = Regex.Match(selector, """^:root\[data-theme="(\w+)"\]$""");
            if (m.Success && m.Groups[1].Value == theme)
            {
                specificity = 2;
                return true;
            }

            return false;
        }

        /// <summary>Top-level and @media-nested rules, in source order.</summary>
        private static IEnumerable<(string? Media, string Selector, string Declarations)> Rules(string css)
        {
            css = Regex.Replace(css, @"/\*.*?\*/", "", RegexOptions.Singleline);
            var i = 0;
            while (i < css.Length)
            {
                var open = css.IndexOf('{', i);
                if (open < 0) yield break;

                var prelude = css[i..open].Trim();
                if (prelude.StartsWith('@'))
                {
                    var depth = 1;
                    var j = open + 1;
                    while (depth > 0 && j < css.Length)
                    {
                        if (css[j] == '{') depth++;
                        else if (css[j] == '}') depth--;
                        j++;
                    }

                    foreach (var (_, sel, decl) in Rules(css[(open + 1)..(j - 1)]))
                        yield return (prelude, sel, decl);

                    i = j;
                }
                else
                {
                    var close = css.IndexOf('}', open);
                    foreach (var selector in prelude.Split(','))
                        yield return (null, selector, css[(open + 1)..close]);

                    i = close + 1;
                }
            }
        }

        private static double Luminance(string colour)
        {
            var hex = colour.TrimStart('#');
            if (hex.Length == 3) hex = string.Concat(hex.Select(c => $"{c}{c}"));

            double Channel(int index) => int.Parse(hex.Substring(index * 2, 2), NumberStyles.HexNumber) / 255.0;

            return 0.2126 * Channel(0) + 0.7152 * Channel(1) + 0.0722 * Channel(2);
        }
    }
}
