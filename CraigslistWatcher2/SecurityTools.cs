using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security;


namespace CraigslistWatcher2
{
    /// <summary>
    /// Used to encrypt/decrypt information
    /// This is mainly used in that project in order to save the login/password information used to connect to the SMTP server to send email
    /// That way the info is not stored in a clear text file. However it can easily be decrypted by the current windows user (assuming someone takes control of your windows user account)
    /// </summary>
    internal static class SecurityTools
    {
        //http://msdn.microsoft.com/en-us/library/system.security.cryptography.dataprotectionscope(v=vs.100).aspx
        static byte[] entropy = System.Text.Encoding.Unicode.GetBytes("Just some salty entropy");
        public static string EncryptString(System.Security.SecureString input)
        {
            byte[] encryptedData = System.Security.Cryptography.ProtectedData.Protect(
                System.Text.Encoding.Unicode.GetBytes(ToInsecureString(input)),
                entropy,
                System.Security.Cryptography.DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encryptedData);
        }
        internal static SecureString DecryptString(string encryptedData)
        {
            try
            {
                byte[] decryptedData = System.Security.Cryptography.ProtectedData.Unprotect(
                    Convert.FromBase64String(encryptedData),
                    entropy,
                    System.Security.Cryptography.DataProtectionScope.CurrentUser);
                return ToSecureString(System.Text.Encoding.Unicode.GetString(decryptedData));
            }
            catch
            {
                return new SecureString();
            }
        }
        internal static SecureString ToSecureString(string input)
        {
            SecureString secure = new SecureString();
            foreach (char c in input)
            {
                secure.AppendChar(c);
            }
            secure.MakeReadOnly();
            return secure;
        }
        internal static string ToInsecureString(SecureString input)
        {
            string returnValue = string.Empty;
            IntPtr ptr = System.Runtime.InteropServices.Marshal.SecureStringToBSTR(input);
            try
            {
                returnValue = System.Runtime.InteropServices.Marshal.PtrToStringBSTR(ptr);
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.ZeroFreeBSTR(ptr);
            }
            return returnValue;
        }
    }
}
