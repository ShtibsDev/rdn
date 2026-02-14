// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Rdn
{
    /// <summary>
    /// Represents an ISO 8601 duration value from RDN (e.g. P1Y2M3DT4H5M6S).
    /// TimeSpan cannot represent years/months, so this struct preserves the original ISO string.
    /// </summary>
    public readonly struct RdnDuration : IEquatable<RdnDuration>
    {
        /// <summary>
        /// Gets the raw ISO 8601 duration string (e.g. "P1Y2M3DT4H5M6S").
        /// </summary>
        public string Iso { get; }

        /// <summary>
        /// Creates a new RdnDuration from an ISO 8601 duration string.
        /// </summary>
        public RdnDuration(string iso)
        {
            ArgumentNullException.ThrowIfNull(iso);
            Iso = iso;
        }

        /// <summary>
        /// Tries to convert this duration to a TimeSpan. Only succeeds if the duration
        /// contains no year or month components.
        /// </summary>
        public bool TryToTimeSpan(out TimeSpan result)
        {
            // Simple durations like PT4H5M6S can be converted; those with Y or M (month) cannot.
            // 'M' before 'T' is months; 'M' after 'T' is minutes — only reject months.
            if (Iso != null && !Iso.Contains('Y'))
            {
                int tIndex = Iso.IndexOf('T');
                ReadOnlySpan<char> datePart = tIndex >= 0 ? Iso.AsSpan(1, tIndex - 1) : Iso.AsSpan(1);
                if (!datePart.Contains('M'))
                {
                    // Parse D, H, M (minute), S components
                    return TryParseDurationToTimeSpan(Iso, out result);
                }
            }

            result = default;
            return false;
        }

        private static bool TryParseDurationToTimeSpan(string iso, out TimeSpan result)
        {
            result = default;
            if (string.IsNullOrEmpty(iso) || iso[0] != 'P')
                return false;

            int days = 0, hours = 0, minutes = 0;
            double seconds = 0;
            int i = 1;
            bool inTimePart = false;

            while (i < iso.Length)
            {
                if (iso[i] == 'T')
                {
                    inTimePart = true;
                    i++;
                    continue;
                }

                int numStart = i;
                while (i < iso.Length && (char.IsDigit(iso[i]) || iso[i] == '.'))
                    i++;

                if (i >= iso.Length || numStart == i)
                    return false;

                char designator = iso[i];
                string numStr = iso.Substring(numStart, i - numStart);
                i++;

                switch (designator)
                {
                    case 'D':
                        if (!int.TryParse(numStr, out days)) return false;
                        break;
                    case 'H':
                        if (!int.TryParse(numStr, out hours)) return false;
                        break;
                    case 'M' when inTimePart:
                        if (!int.TryParse(numStr, out minutes)) return false;
                        break;
                    case 'S':
                        if (!double.TryParse(numStr, System.Globalization.CultureInfo.InvariantCulture, out seconds)) return false;
                        break;
                    case 'W':
                        if (!int.TryParse(numStr, out int weeks)) return false;
                        days += weeks * 7;
                        break;
                    default:
                        return false;
                }
            }

            result = new TimeSpan(days, hours, minutes, 0) + TimeSpan.FromSeconds(seconds);
            return true;
        }

        public override string ToString() => Iso ?? "";

        public bool Equals(RdnDuration other) => string.Equals(Iso, other.Iso, StringComparison.Ordinal);

        public override bool Equals(object? obj) => obj is RdnDuration other && Equals(other);

        public override int GetHashCode() => Iso?.GetHashCode(StringComparison.Ordinal) ?? 0;

        public static bool operator ==(RdnDuration left, RdnDuration right) => left.Equals(right);

        public static bool operator !=(RdnDuration left, RdnDuration right) => !left.Equals(right);
    }
}
