using System;
using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;

namespace RadialLauncher.Services.Intelligence
{
    public class CalculatorResult
    {
        public bool IsValid { get; set; }
        public string Expression { get; set; } = string.Empty;
        public string FormattedResult { get; set; } = string.Empty;
        public string RawValue { get; set; } = string.Empty;
    }

    public interface IInlineCalculatorService
    {
        CalculatorResult TryEvaluate(string query);
    }

    public class InlineCalculatorService : IInlineCalculatorService
    {
        private static readonly Regex UnitConversionRegex = new(
            @"^\s*(-?\d+(?:[.,]\d+)?)\s*(km|mi|gb|mb|tb|kg|lb|c|f|usd|try|hr|saat|dk|min)\s*(?:to|in|->|=)?\s*(km|mi|gb|mb|tb|kg|lb|c|f|usd|try|hr|saat|dk|min)\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex SqrtRegex = new(
            @"sqrt\s*\(\s*([^()]+)\s*\)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex PowerRegex = new(
            @"(-?\d+(?:\.\d+)?)\s*\^\s*(-?\d+(?:\.\d+)?)",
            RegexOptions.Compiled);

        private static readonly Regex AllowedMathCharsRegex = new(
            @"^[0-9+\-*/%^().,\s]+$",
            RegexOptions.Compiled);

        private static readonly Regex OperatorOrFuncRegex = new(
            @"[+\-*/%^]|sqrt\s*\(",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex PlainNumberRegex = new(
            @"^\s*-?\d+(?:[.,]\d+)?\s*$",
            RegexOptions.Compiled);

        public CalculatorResult TryEvaluate(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new CalculatorResult { IsValid = false };

            string trimmed = query.Trim();
            if (trimmed.StartsWith("="))
                trimmed = trimmed.Substring(1).Trim();

            var convMatch = UnitConversionRegex.Match(trimmed);
            if (convMatch.Success)
            {
                return EvaluateUnitConversion(trimmed, convMatch);
            }

            if (PlainNumberRegex.IsMatch(trimmed) || !OperatorOrFuncRegex.IsMatch(trimmed))
                return new CalculatorResult { IsValid = false };

            return EvaluateMathExpression(trimmed);
        }

        private static CalculatorResult EvaluateUnitConversion(string original, Match match)
        {
            string numStr = match.Groups[1].Value.Replace(',', '.');
            if (!double.TryParse(numStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double val))
                return new CalculatorResult { IsValid = false };

            string from = NormalizeUnit(match.Groups[2].Value);
            string to = NormalizeUnit(match.Groups[3].Value);

            double? converted = (from, to) switch
            {
                ("km", "mi") => val * 0.621371,
                ("mi", "km") => val * 1.60934,
                ("gb", "mb") => val * 1024.0,
                ("mb", "gb") => val / 1024.0,
                ("tb", "gb") => val * 1024.0,
                ("gb", "tb") => val / 1024.0,
                ("tb", "mb") => val * 1048576.0,
                ("mb", "tb") => val / 1048576.0,
                ("kg", "lb") => val * 2.20462,
                ("lb", "kg") => val / 2.20462,
                ("c", "f") => (val * 9.0 / 5.0) + 32.0,
                ("f", "c") => (val - 32.0) * 5.0 / 9.0,
                ("usd", "try") => val * 38.50,
                ("try", "usd") => val / 38.50,
                ("hr", "min") => val * 60.0,
                ("min", "hr") => val / 60.0,
                _ when from == to => val,
                _ => null
            };

            if (converted == null || double.IsNaN(converted.Value) || double.IsInfinity(converted.Value))
                return new CalculatorResult { IsValid = false };

            string raw = FormatNumber(converted.Value);
            string unitLabel = GetDisplayUnit(to, match.Groups[3].Value);
            return new CalculatorResult
            {
                IsValid = true,
                Expression = original,
                RawValue = raw,
                FormattedResult = $"{raw} {unitLabel}"
            };
        }

        private static string NormalizeUnit(string unit)
        {
            string u = unit.ToLowerInvariant();
            return u switch
            {
                "saat" => "hr",
                "dk" => "min",
                _ => u
            };
        }

        private static string GetDisplayUnit(string normalizedUnit, string rawUnit)
        {
            if (rawUnit.Equals("dk", StringComparison.OrdinalIgnoreCase)) return "dk";
            if (rawUnit.Equals("saat", StringComparison.OrdinalIgnoreCase)) return "saat";
            return normalizedUnit.ToUpperInvariant();
        }

        private static CalculatorResult EvaluateMathExpression(string expression)
        {
            try
            {
                string working = expression.Replace(',', '.');

                int safety = 0;
                while (SqrtRegex.IsMatch(working) && safety++ < 10)
                {
                    working = SqrtRegex.Replace(working, m =>
                    {
                        double innerVal = ComputeRaw(m.Groups[1].Value);
                        if (double.IsNaN(innerVal) || innerVal < 0)
                            throw new InvalidOperationException("Invalid sqrt argument");
                        return Math.Sqrt(innerVal).ToString(CultureInfo.InvariantCulture);
                    });
                }

                if (!AllowedMathCharsRegex.IsMatch(working))
                    return new CalculatorResult { IsValid = false };

                safety = 0;
                while (PowerRegex.IsMatch(working) && safety++ < 15)
                {
                    working = PowerRegex.Replace(working, m =>
                    {
                        double b = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                        double exp = double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
                        return Math.Pow(b, exp).ToString(CultureInfo.InvariantCulture);
                    }, 1);
                }

                double result = ComputeRaw(working);
                if (double.IsNaN(result) || double.IsInfinity(result))
                    return new CalculatorResult { IsValid = false };

                string formatted = FormatNumber(result);
                return new CalculatorResult
                {
                    IsValid = true,
                    Expression = expression,
                    RawValue = result.ToString("0.######", CultureInfo.InvariantCulture),
                    FormattedResult = formatted
                };
            }
            catch
            {
                return new CalculatorResult { IsValid = false };
            }
        }

        private static double ComputeRaw(string expr)
        {
            using var table = new DataTable { Locale = CultureInfo.InvariantCulture };
            object? val = table.Compute(expr, string.Empty);
            if (val == null || val == DBNull.Value)
                return double.NaN;
            return Convert.ToDouble(val, CultureInfo.InvariantCulture);
        }

        private static string FormatNumber(double value)
        {
            if (Math.Abs(value - Math.Round(value)) < 1e-9)
                return Math.Round(value).ToString("#,0", CultureInfo.InvariantCulture);
            return value.ToString("#,0.####", CultureInfo.InvariantCulture);
        }
    }
}
