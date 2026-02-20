namespace EvolveOS_Optimizer.Utilities.Extensions
{
    public static class StringExtensions
    {
        public static string? GetMessage(this Exception obj)
        {
            if (obj == null) return null;

            var exception = obj;
            var messages = new List<string>();

            do
            {
                try
                {
                    var message = exception.Message;

                    if (!string.IsNullOrEmpty(message))
                    {
                        messages.Add(message.Trim());
                    }
                    else
                    {
                        messages.Add(exception.ToString());
                    }
                }
                catch
                {
                    messages.Add(exception.ToString());
                }

                exception = exception.InnerException;
            }
            while (exception != null);

            return string.Join(". ", messages.Distinct());
        }

        public static string RemoveWhitespaces(this string obj)
        {
            if (string.IsNullOrEmpty(obj)) return obj;
            return new string(obj.ToCharArray().Where(c => !char.IsWhiteSpace(c)).ToArray());
        }
    }
}