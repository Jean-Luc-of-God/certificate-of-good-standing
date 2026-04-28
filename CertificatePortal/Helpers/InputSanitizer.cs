namespace CertificatePortal.Helpers
{
    public static class InputSanitizer
    {
        public static string Sanitize(string input, int maxLength = 100)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            
            var trimmed = input.Trim();
            return trimmed.Length > maxLength ? trimmed.Substring(0, maxLength) : trimmed;
        }
    }
}
