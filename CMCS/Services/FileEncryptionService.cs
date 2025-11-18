using System.Security.Cryptography;
using System.Text;

namespace CMCS.Services
{
    public interface IFileEncryptionService
    {
        Task<byte[]> EncryptFileAsync(IFormFile file);
        byte[] DecryptFile(byte[] encryptedData);
        string GetEncryptionKey();
    }

    public class FileEncryptionService : IFileEncryptionService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<FileEncryptionService> _logger;

        public FileEncryptionService(IConfiguration configuration, ILogger<FileEncryptionService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Encrypts an uploaded file using AES encryption
        /// </summary>
        public async Task<byte[]> EncryptFileAsync(IFormFile file)
        {
            try
            {
                // Read file into byte array
                using (var memoryStream = new MemoryStream())
                {
                    await file.CopyToAsync(memoryStream);
                    byte[] fileData = memoryStream.ToArray();

                    // Encrypt and return
                    return EncryptData(fileData);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error encrypting file: {FileName}", file.FileName);
                throw;
            }
        }

        /// <summary>
        /// Decrypts encrypted file data
        /// </summary>
        public byte[] DecryptFile(byte[] encryptedData)
        {
            try
            {
                return DecryptData(encryptedData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error decrypting file");
                throw;
            }
        }

        /// <summary>
        /// Gets or creates a consistent encryption key
        /// </summary>
        public string GetEncryptionKey()
        {
            var key = _configuration["Encryption:Key"];
            if (string.IsNullOrEmpty(key))
            {
                _logger.LogWarning("Encryption key not found in configuration. Using default key (NOT RECOMMENDED for production)");
                key = "DefaultEncryptionKeyFor32BytesLength1234"; // 32 bytes for AES-256
            }
            return key;
        }

        /// <summary>
        /// Encrypts byte data using AES encryption
        /// </summary>
        private byte[] EncryptData(byte[] data)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = Encoding.UTF8.GetBytes(GetEncryptionKey().PadRight(32).Substring(0, 32)); // 32 bytes for AES-256
                aes.GenerateIV();

                using (var encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
                using (var ms = new MemoryStream())
                {
                    // Write IV to the beginning of the stream (needed for decryption)
                    ms.Write(aes.IV, 0, aes.IV.Length);

                    using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    {
                        cs.Write(data, 0, data.Length);
                        cs.FlushFinalBlock();
                    }

                    return ms.ToArray();
                }
            }
        }

        /// <summary>
        /// Decrypts byte data using AES decryption
        /// </summary>
        private byte[] DecryptData(byte[] encryptedData)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = Encoding.UTF8.GetBytes(GetEncryptionKey().PadRight(32).Substring(0, 32)); // 32 bytes for AES-256

                // Extract IV from the beginning of the encrypted data
                byte[] iv = new byte[aes.IV.Length];
                Array.Copy(encryptedData, 0, iv, 0, iv.Length);
                aes.IV = iv;

                using (var decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
                using (var ms = new MemoryStream(encryptedData, iv.Length, encryptedData.Length - iv.Length))
                using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                using (var resultMs = new MemoryStream())
                {
                    cs.CopyTo(resultMs);
                    return resultMs.ToArray();
                }
            }
        }
    }
}
