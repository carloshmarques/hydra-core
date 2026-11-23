using System;
using System.Data.SQLite;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Hydra.Kernel
{
    public class CryptoVaultService
    {
        private const int SaltSize = 16;
        private const int IvSize = 16;
        private const int HmacSize = 32;
        private const int KeySize = 32; // 256-bit AES
        private const int Iterations = 480000; // OWASP recommendation

        public string VaultDirectory { get; }
        public string VaultFilePath => Path.Combine(VaultDirectory, "auth.vault");
        public bool HasVault => File.Exists(VaultFilePath);

        public CryptoVaultService()
        {
            VaultDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Hydra");
        }

        private byte[] DeriveKey(string password, byte[] salt)
        {
            using (var kdf = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256))
            {
                return kdf.GetBytes(KeySize);
            }
        }

        public void EncryptBytesToFile(byte[] plainBytes, string outputPath, string password)
        {
            byte[] salt = new byte[SaltSize];
            byte[] iv = new byte[IvSize];

            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
                rng.GetBytes(iv);
            }

            byte[] masterKey = DeriveKey(password, salt);
            byte[] cipherBytes;

            using (var aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = masterKey;
                aes.IV = iv;

                using (var encryptor = aes.CreateEncryptor())
                {
                    cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
                }
            }

            byte[] payload = new byte[SaltSize + IvSize + cipherBytes.Length];
            Buffer.BlockCopy(salt, 0, payload, 0, SaltSize);
            Buffer.BlockCopy(iv, 0, payload, SaltSize, IvSize);
            Buffer.BlockCopy(cipherBytes, 0, payload, SaltSize + IvSize, cipherBytes.Length);

            byte[] hmac;
            using (var hmacSha256 = new HMACSHA256(masterKey))
            {
                hmac = hmacSha256.ComputeHash(payload);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            using (var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
            {
                fs.Write(payload, 0, payload.Length);
                fs.Write(hmac, 0, hmac.Length);
            }
        }

        public byte[] DecryptFileToBytes(string encryptedPath, string password)
        {
            byte[] fileData = File.ReadAllBytes(encryptedPath);
            int headerAndHmacLen = SaltSize + IvSize + HmacSize;

            if (fileData.Length <= headerAndHmacLen)
                throw new CryptographicException("Invalid or corrupted vault file.");

            int payloadLen = fileData.Length - HmacSize;
            byte[] payload = new byte[payloadLen];
            byte[] storedHmac = new byte[HmacSize];

            Buffer.BlockCopy(fileData, 0, payload, 0, payloadLen);
            Buffer.BlockCopy(fileData, payloadLen, storedHmac, 0, HmacSize);

            byte[] salt = new byte[SaltSize];
            byte[] iv = new byte[IvSize];
            int cipherLen = payloadLen - SaltSize - IvSize;
            byte[] cipherBytes = new byte[cipherLen];

            Buffer.BlockCopy(payload, 0, salt, 0, SaltSize);
            Buffer.BlockCopy(payload, SaltSize, iv, 0, IvSize);
            Buffer.BlockCopy(payload, SaltSize + IvSize, cipherBytes, 0, cipherLen);

            byte[] masterKey = DeriveKey(password, salt);

            using (var hmacSha256 = new HMACSHA256(masterKey))
            {
                byte[] computedHmac = hmacSha256.ComputeHash(payload);
                if (!FixedTimeEquals(storedHmac, computedHmac))
                    throw new CryptographicException("Authentication failed: invalid credentials.");
            }

            using (var aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = masterKey;
                aes.IV = iv;

                using (var decryptor = aes.CreateDecryptor())
                {
                    return decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
                }
            }
        }

        public void InitializeFirstSuperUser(string username, string password, string sqlitePath)
        {
            string cleanUser = username.Trim();

            var payloadObj = new JObject
            {
                ["username"] = cleanUser,
                ["role"] = "su",
                ["created_at"] = DateTime.UtcNow.ToString("o")
            };
            byte[] plainBytes = Encoding.UTF8.GetBytes(payloadObj.ToString(Formatting.None));
            EncryptBytesToFile(plainBytes, VaultFilePath, password);

            if (File.Exists(sqlitePath))
            {
                string connString = $"Data Source={sqlitePath};Version=3;";
                using (var conn = new SQLiteConnection(connString))
                {
                    conn.Open();
                    string sql = @"
                        INSERT OR REPLACE INTO utilizadores (utilizador_id, username, nome_exibicao, perfil, ativo, ultimo_login)
                        VALUES (1, @usr, @disp, 'su', 1, @dt);";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@usr", cleanUser);
                        cmd.Parameters.AddWithValue("@disp", cleanUser);
                        cmd.Parameters.AddWithValue("@dt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        public bool AuthenticateAndTouchSqlite(string username, string password, string sqlitePath)
        {
            if (!HasVault) return false;

            try
            {
                string cleanUser = username.Trim();
                byte[] plainBytes = DecryptFileToBytes(VaultFilePath, password);
                JObject payloadObj = JObject.Parse(Encoding.UTF8.GetString(plainBytes));
                string storedUser = payloadObj["username"]?.ToString() ?? string.Empty;

                if (!storedUser.Equals(cleanUser, StringComparison.OrdinalIgnoreCase))
                    return false;

                if (File.Exists(sqlitePath))
                {
                    string connString = $"Data Source={sqlitePath};Version=3;";
                    using (var conn = new SQLiteConnection(connString))
                    {
                        conn.Open();
                        string sql = "UPDATE utilizadores SET ultimo_login = @dt WHERE LOWER(username) = LOWER(@usr) AND ativo = 1;";
                        using (var cmd = new SQLiteCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("@dt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                            cmd.Parameters.AddWithValue("@usr", cleanUser);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++)
                diff |= a[i] ^ b[i];
            return diff == 0;
        }
    }
}
