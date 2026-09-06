using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Windows;
using RadialLauncher.Services.Actions;
using RadialLauncher.Services.Localization;
using RadialLauncher.Services.Themes;
using Serilog;

namespace RadialLauncher.Services.Commands
{
    public class CommandPaletteService : ICommandPaletteService
    {
        private readonly ISystemActionService _systemActionService;
        private readonly IThemeService _themeService;

        public CommandPaletteService(ISystemActionService systemActionService, IThemeService themeService)
        {
            _systemActionService = systemActionService ?? throw new ArgumentNullException(nameof(systemActionService));
            _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
        }

        public bool TryHandle(string query, out string message)
        {
            message = string.Empty;
            if (string.IsNullOrWhiteSpace(query)) return false;

            string q = query.Trim();
            bool isTr = LocalizationService.Instance.CurrentLanguage == "tr";

            if (q.StartsWith("=", StringComparison.Ordinal))
            {
                string expr = q.Substring(1).Trim();
                return TryMath(expr, isTr, out message);
            }

            if (q.StartsWith(">", StringComparison.Ordinal))
            {
                string action = q.Substring(1).Trim();
                if (string.IsNullOrEmpty(action))
                {
                    message = isTr ? "Güç komutları: >kilitle, >uyku, >kapat, >yeniden, >çöp" : "Power commands: >lock, >sleep, >shutdown, >restart, >recycle";
                    return true;
                }
                return TryPowerAction(action, isTr, out message);
            }

            if (q.StartsWith("!", StringComparison.Ordinal) || q.StartsWith("?", StringComparison.Ordinal))
            {
                string search = q.Substring(1).Trim();
                if (string.IsNullOrEmpty(search))
                {
                    message = isTr ? "Web araması: !google veya ?aradığınız" : "Web search: !google or ?search_query";
                    return true;
                }
                return TryWebSearch(search, isTr, out message);
            }

            return false;
        }

        private bool TryMath(string expr, bool isTr, out string message)
        {
            message = string.Empty;
            if (string.IsNullOrEmpty(expr))
            {
                message = isTr ? "Hesaplama girin. Örn: =2+3*4" : "Enter calculation. E.g.: =2+3*4";
                return true;
            }

            try
            {
                double value = Evaluate(expr);
                if (double.IsNaN(value) || double.IsInfinity(value))
                {
                    message = isTr ? "Geçersiz matematiksel sonuç." : "Invalid mathematical result.";
                    return true;
                }
                var ci = CultureInfo.InvariantCulture;
                string num = value.ToString(value == Math.Floor(value) ? "N0" : "G6", ci);
                CopyToClipboardSilent(num);
                message = isTr ? $"= {expr} → {num} (panoya kopyalandı)" : $"= {expr} → {num} (copied to clipboard)";
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Math evaluation failed for {Expr}", expr);
                message = isTr ? $"İfade hesaplanamadı: {expr}" : $"Could not evaluate: {expr}";
            }
            return true;
        }

        private double Evaluate(string expr)
        {
            var rpn = new List<string>();
            var ops = new Stack<string>();
            var num = new StringBuilder();
            bool lastWasValue = false;

            void EmitNum()
            {
                if (num.Length > 0)
                {
                    string t = num.ToString();
                    if (!double.TryParse(t.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
                        throw new FormatException($"Invalid number: {t}");
                    rpn.Add(v.ToString("R", CultureInfo.InvariantCulture));
                    num.Clear();
                    lastWasValue = true;
                }
            }

            int prec(string o) => o switch { "+" or "-" => 1, "*" or "/" => 2, "^" => 3, _ => 0 };

            foreach (char c in expr)
            {
                if (char.IsDigit(c) || c == '.' || c == ',')
                {
                    num.Append(c);
                    lastWasValue = false;
                }
                else if (c == '+' || c == '-' || c == '*' || c == '/' || c == '^' || c == '(' || c == ')')
                {
                    if ((c == '+' || c == '-') && (num.Length == 0) && (!lastWasValue) && rpn.Count == 0 && ops.Count == 0)
                    {
                        num.Append('0');
                        num.Append(c);
                        continue;
                    }
                    EmitNum();
                    if (c == '(')
                    {
                        ops.Push("(");
                        lastWasValue = false;
                    }
                    else if (c == ')')
                    {
                        while (ops.Count > 0 && ops.Peek() != "(")
                            rpn.Add(ops.Pop());
                        if (ops.Count > 0 && ops.Peek() == "(") ops.Pop();
                        lastWasValue = true;
                    }
                    else
                    {
                        string op = c.ToString();
                        while (ops.Count > 0 && ops.Peek() != "(" && prec(ops.Peek()) >= prec(op))
                            rpn.Add(ops.Pop());
                        ops.Push(op);
                        lastWasValue = false;
                    }
                }
            }
            EmitNum();
            while (ops.Count > 0) rpn.Add(ops.Pop());

            var stack = new Stack<double>();
            foreach (string tok in rpn)
            {
                if (double.TryParse(tok, NumberStyles.Float, CultureInfo.InvariantCulture, out double val))
                {
                    stack.Push(val);
                }
                else
                {
                    if (stack.Count < 2) throw new FormatException("Expression incomplete");
                    double b = stack.Pop();
                    double a = stack.Pop();
                    double r = tok switch
                    {
                        "+" => a + b,
                        "-" => a - b,
                        "*" => a * b,
                        "/" => a / b,
                        "^" => Math.Pow(a, b),
                        _ => throw new FormatException($"Unknown operator: {tok}")
                    };
                    stack.Push(r);
                }
            }
            if (stack.Count != 1) throw new FormatException("Expression incomplete");
            return stack.Pop();
        }

        private bool TryPowerAction(string action, bool isTr, out string message)
        {
            message = string.Empty;
            string a = action.ToLowerInvariant();

            switch (a)
            {
                case "kilitle":
                case "lock":
                    _systemActionService.ExecuteAction("LOCK_PC");
                    message = isTr ? "Bilgisayar kilitlendi." : "Workstation locked.";
                    break;
                case "uyku":
                case "sleep":
                    Process.Start(new ProcessStartInfo("rundll32.exe", "powrprof.dll,SetSuspendState 0,1,0") { UseShellExecute = true });
                    message = isTr ? "Uyku moduna geçiliyor..." : "Entering sleep mode...";
                    break;
                case "kapat":
                case "shutdown":
                    Process.Start(new ProcessStartInfo("shutdown.exe", "/s /t 0") { UseShellExecute = false, CreateNoWindow = true });
                    message = isTr ? "Bilgisayar kapatılıyor..." : "Shutting down system...";
                    break;
                case "yeniden":
                case "restart":
                case "reboot":
                    Process.Start(new ProcessStartInfo("shutdown.exe", "/r /t 0") { UseShellExecute = false, CreateNoWindow = true });
                    message = isTr ? "Yeniden başlatılıyor..." : "Restarting system...";
                    break;
                case "çöp":
                case "cop":
                case "bin":
                case "recycle":
                    _systemActionService.ExecuteAction("EMPTY_RECYCLE_BIN");
                    message = isTr ? "Geri dönüşüm kutusu boşaltıldı." : "Recycle bin emptied.";
                    break;
                case "masaüstü":
                case "masaustu":
                case "desktop":
                    _systemActionService.ExecuteAction("SHOW_DESKTOP");
                    message = isTr ? "Masaüstü gösterildi." : "Desktop displayed.";
                    break;
                case "koyu":
                case "dark":
                    _themeService.SetCurrentTheme("Dark");
                    message = isTr ? "Koyu tema aktif." : "Dark theme activated.";
                    break;
                case "açık":
                case "acik":
                case "light":
                case "white":
                    _themeService.SetCurrentTheme("White");
                    message = isTr ? "Açık tema aktif." : "Light theme activated.";
                    break;
                default:
                    message = isTr ? $"Bilinmeyen güç/sistem komutu: {a}" : $"Unknown system command: {a}";
                    return true;
            }
            return true;
        }

        private bool TryWebSearch(string term, bool isTr, out string message)
        {
            message = string.Empty;
            string url = "https://www.google.com/search?q=" + Uri.EscapeDataString(term);
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                message = isTr ? $"Web araması başlatıldı: {term}" : $"Web search launched: {term}";
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to open web search for {Term}", term);
                message = isTr ? $"Tarayıcı açılamadı: {term}" : $"Failed opening browser: {term}";
            }
            return true;
        }

        private void CopyToClipboardSilent(string text)
        {
            try
            {
                System.Windows.Clipboard.SetText(text);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Clipboard write failed in command palette");
            }
        }
    }
}
