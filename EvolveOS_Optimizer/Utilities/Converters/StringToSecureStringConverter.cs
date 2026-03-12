using System.Security;

namespace EvolveOS_Optimizer.Utilities.Converters
{
    public static class StringToSecureStringConverter
    {
        public static SecureString ConvertToSecureString(string plainString)
        {
            if (string.IsNullOrEmpty(plainString))
            {
                return new SecureString();
            }

            SecureString secureString = new SecureString();

            foreach (char c in plainString)
            {
                secureString.AppendChar(c);
            }

            secureString.MakeReadOnly();
            return secureString;
        }
    }
}
