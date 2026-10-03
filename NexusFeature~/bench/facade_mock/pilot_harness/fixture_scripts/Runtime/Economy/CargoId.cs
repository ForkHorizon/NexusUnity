namespace Saltmarsh.Economy
{
    public readonly struct CargoId
    {
        public readonly string Kind;
        public readonly int Number;

        public CargoId(string kind, int number) { Kind = kind; Number = number; }

        public static bool TryParse(string s, out CargoId id)
        {
            id = default;
            if (string.IsNullOrEmpty(s)) return false;
            int us = s.LastIndexOf('_');
            if (us <= 0 || us == s.Length - 1) return false;
            if (!int.TryParse(s.Substring(us + 1), out var n)) return false;
            id = new CargoId(s.Substring(0, us), n);
            return true;
        }

        public override string ToString() => Kind + "_" + Number.ToString("00");
    }
}
