// ZipStorer, by Jaime Olivares
// Website: http://github.com/jaime-olivares/zipstorer

using System.Security;
using System.Security.Cryptography;

namespace System.IO.Compression
{
    public class ZipEncryptor
    {
        private const int KeySize = 256; // AES-256
        private const int BlockSize = 16; // AES Block Size (128 bits)
        private const int SaltSize = 16; // Salt size for PBKDF2

        private static RandomNumberGenerator randomGenerator = RandomNumberGenerator.Create();

        public static byte[] EncryptZipContent(byte[] data, string password)
        {
            byte[] salt = new byte[SaltSize];
            randomGenerator.GetBytes(salt);

            // Derive key and IV from password and salt
            using (var keyDerivation = new Rfc2898DeriveBytes(password, salt, 100000))
            {
                byte[] key = keyDerivation.GetBytes(KeySize / 8);
                byte[] iv = new byte[BlockSize];

                randomGenerator.GetBytes(iv);

                using (var aes = Aes.Create())
                {
                    aes.KeySize = KeySize;
                    aes.BlockSize = BlockSize * 8;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    aes.Key = key;
                    aes.IV = iv;

                    using (var encryptor = aes.CreateEncryptor())
                    using (var ms = new MemoryStream())
                    {
                        ms.Write(salt, 0, salt.Length);
                        ms.Write(iv, 0, iv.Length);

                        using (var cryptoStream = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                        {
                            cryptoStream.Write(data, 0, data.Length);
                            cryptoStream.FlushFinalBlock();
                        }

                        return ms.ToArray();
                    }
                }
            }
        }

        public static SecureString SecurePassword(string password)
        {
            SecureString securePassword = new SecureString();
            
            foreach (char c in password)
            {
                securePassword.AppendChar(c);
            }

            securePassword.MakeReadOnly();

            return securePassword;
        }
    }
}

/* Example usage
class Program
{
    static void Main()
    {
        string password = "StrongPassword123!";
        byte[] originalData = Encoding.UTF8.GetBytes("This is a ZIP file entry content.");
        byte[] encryptedData = ZipEncryptor.EncryptZipEntry(originalData, password);
        Console.WriteLine("Encrypted ZIP entry (Base64): " + Convert.ToBase64String(encryptedData));
    }
} */