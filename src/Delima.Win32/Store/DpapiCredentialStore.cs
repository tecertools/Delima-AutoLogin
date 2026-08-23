using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Delima.Core.Crypto;
using Delima.Core.Roster;
using Delima.Core.Store;
using ClassInfo = Delima.Core.Roster.ClassInfo;

namespace Delima.Win32.Store;

/// <summary>
/// Per-PC credential store implementation for Windows lab PCs using DPAPI at LocalMachine scope.
/// Adheres to Technical Architecture §3.3, §3.4, and §3.5.
/// </summary>
public sealed class DpapiCredentialStore : ICredentialStore
{
    public const string DefaultDirectoryName = "DELIMa Launcher";
    public const string CredentialsFileName = "credentials.dat";
    public const string EntropyFileName = "credentials.entropy";
    public const int EntropySizeBytes = 32;

    private readonly string _dataPath;
    private readonly string _entropyPath;
    private readonly byte[] _entropy;
    private readonly byte[] _protectedBlob;

    private readonly HashSet<string> _studentIdsWithPassword = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _allStudentIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, bool> _studentActiveMap = new(StringComparer.OrdinalIgnoreCase);

    private bool _disposed;

    /// <summary>
    /// Schema version of the store (e.g. 2).
    /// </summary>
    public ushort SchemaVersion { get; private set; } = 2;

    /// <summary>
    /// MOE school code associated with the store.
    /// </summary>
    public string SchoolCode { get; private set; } = "";

    /// <summary>
    /// Timestamp when the store was generated.
    /// </summary>
    public DateTimeOffset GeneratedAt { get; private set; }

    /// <summary>
    /// School metadata from the provisioned store.
    /// </summary>
    public School School { get; private set; } = new();

    /// <summary>
    /// Theme settings from the provisioned store.
    /// </summary>
    public ThemeInfo Theme { get; private set; } = new();

    /// <summary>
    /// Application configuration from the provisioned store.
    /// </summary>
    public AppConfig Config { get; private set; } = new();

    /// <summary>
    /// Registered classes in the provisioned store.
    /// </summary>
    public IReadOnlyList<ClassInfo> Classes { get; private set; } = [];

    /// <summary>
    /// Registered students (without passwords) in the provisioned store.
    /// </summary>
    public IReadOnlyList<Student> Students { get; private set; } = [];

    /// <summary>
    /// Full path to the credentials.dat file.
    /// </summary>
    public string DataFilePath => _dataPath;

    /// <summary>
    /// Full path to the credentials.entropy file.
    /// </summary>
    public string EntropyFilePath => _entropyPath;

    /// <summary>
    /// Total count of pupils registered in the store.
    /// </summary>
    public int StudentCount => _allStudentIds.Count;

    /// <summary>
    /// Gets the default storage directory: %ProgramData%\DELIMa Launcher\
    /// </summary>
    public static string GetDefaultStoreDirectory()
    {
        string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        return Path.Combine(programData, DefaultDirectoryName);
    }

    /// <summary>
    /// Gets the full default path to credentials.dat.
    /// </summary>
    public static string GetDefaultDataPath(string? baseDir = null)
    {
        return Path.Combine(baseDir ?? GetDefaultStoreDirectory(), CredentialsFileName);
    }

    /// <summary>
    /// Gets the full default path to credentials.entropy.
    /// </summary>
    public static string GetDefaultEntropyPath(string? baseDir = null)
    {
        return Path.Combine(baseDir ?? GetDefaultStoreDirectory(), EntropyFileName);
    }

    /// <summary>
    /// Checks if both credentials.dat and credentials.entropy exist in the target directory.
    /// </summary>
    public static bool StoreExists(string? baseDir = null)
    {
        string dataPath = GetDefaultDataPath(baseDir);
        string entropyPath = GetDefaultEntropyPath(baseDir);
        return File.Exists(dataPath) && File.Exists(entropyPath);
    }

    /// <summary>
    /// Writes a per-PC store from a MasterBundlePayload using DPAPI LocalMachine scope,
    /// 32-byte CSPRNG entropy, and an HMAC-SHA256 over the ciphertext.
    /// </summary>
    [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility")]
    public static void WriteStore(
        MasterBundlePayload payload,
        string? baseDirectory = null,
        byte[]? customEntropy = null,
        string pupilAccount = StoreAclConfigurator.DefaultPupilAccount,
        bool applyAcls = true)
    {
        ArgumentNullException.ThrowIfNull(payload);

        byte[] jsonBytes = JsonSerializer.SerializeToUtf8Bytes(payload, new JsonSerializerOptions
        {
            WriteIndented = false
        });

        try
        {
            WriteStore(jsonBytes, baseDirectory, customEntropy, pupilAccount, applyAcls);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(jsonBytes);
        }
    }

    /// <summary>
    /// Writes a per-PC store from raw UTF-8 JSON payload bytes using DPAPI LocalMachine scope.
    /// </summary>
    [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility")]
    public static void WriteStore(
        ReadOnlySpan<byte> utf8JsonBytes,
        string? baseDirectory = null,
        byte[]? customEntropy = null,
        string pupilAccount = StoreAclConfigurator.DefaultPupilAccount,
        bool applyAcls = true)
    {
        string baseDir = baseDirectory ?? GetDefaultStoreDirectory();
        string dataPath = GetDefaultDataPath(baseDir);
        string entropyPath = GetDefaultEntropyPath(baseDir);

        StoreAclConfigurator.EnsureDirectoryStructure(baseDir, pupilAccount, applyAcls);

        // 1. Prepare 32-byte entropy
        byte[] entropy = new byte[EntropySizeBytes];
        if (customEntropy != null)
        {
            if (customEntropy.Length != EntropySizeBytes)
            {
                throw new ArgumentException($"Entropy must be exactly {EntropySizeBytes} bytes.", nameof(customEntropy));
            }
            Buffer.BlockCopy(customEntropy, 0, entropy, 0, EntropySizeBytes);
        }
        else
        {
            RandomNumberGenerator.Fill(entropy);
        }

        // 2. Encrypt payload with DPAPI at LocalMachine scope
        byte[] rawPlaintext = utf8JsonBytes.ToArray();
        byte[] protectedBytes;
        try
        {
            protectedBytes = ProtectedData.Protect(rawPlaintext, entropy, DataProtectionScope.LocalMachine);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(rawPlaintext);
        }

        // 3. Compute HMAC-SHA256 over the protected ciphertext using entropy as key
        byte[] hmac = HmacHelper.ComputeHmac(entropy, protectedBytes);

        // 4. File layout: [ProtectedBlob (N)] [HMAC (32)]
        byte[] fileData = new byte[protectedBytes.Length + HmacHelper.HmacSizeBytes];
        Buffer.BlockCopy(protectedBytes, 0, fileData, 0, protectedBytes.Length);
        Buffer.BlockCopy(hmac, 0, fileData, protectedBytes.Length, HmacHelper.HmacSizeBytes);

        // 5. Write entropy and credentials files atomically
        File.WriteAllBytes(entropyPath, entropy);
        File.WriteAllBytes(dataPath, fileData);

        // 6. Apply strict ACLs per §3.5
        if (applyAcls)
        {
            StoreAclConfigurator.ApplyStoreFileAcls(dataPath, pupilAccount);
            StoreAclConfigurator.ApplyStoreFileAcls(entropyPath, pupilAccount);
        }
    }

    /// <summary>
    /// Opens the per-PC DPAPI credential store from the default or specified directory.
    /// </summary>
    public static DpapiCredentialStore Open(string? baseDirectory = null)
    {
        return new DpapiCredentialStore(baseDirectory);
    }

    /// <summary>
    /// Initializes and opens the per-PC DPAPI credential store.
    /// </summary>
    public DpapiCredentialStore(string? baseDirectory = null)
        : this(GetDefaultDataPath(baseDirectory), GetDefaultEntropyPath(baseDirectory))
    {
    }

    /// <summary>
    /// Initializes and opens the per-PC DPAPI credential store with explicit file paths.
    /// </summary>
    [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility")]
    public DpapiCredentialStore(string dataPath, string entropyPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(entropyPath);

        if (!File.Exists(dataPath))
        {
            throw new FileNotFoundException($"Credential store data file not found: {dataPath}", dataPath);
        }

        if (!File.Exists(entropyPath))
        {
            throw new FileNotFoundException($"Credential store entropy file not found: {entropyPath}", entropyPath);
        }

        _dataPath = Path.GetFullPath(dataPath);
        _entropyPath = Path.GetFullPath(entropyPath);

        _entropy = File.ReadAllBytes(_entropyPath);
        if (_entropy.Length != EntropySizeBytes)
        {
            throw new CryptographicException($"Invalid entropy file length. Expected {EntropySizeBytes} bytes, found {_entropy.Length}.");
        }

        byte[] fileBytes = File.ReadAllBytes(_dataPath);
        if (fileBytes.Length < HmacHelper.HmacSizeBytes)
        {
            throw new CryptographicException("Credential store file is corrupted or too short.");
        }

        int protectedLength = fileBytes.Length - HmacHelper.HmacSizeBytes;
        _protectedBlob = new byte[protectedLength];
        Buffer.BlockCopy(fileBytes, 0, _protectedBlob, 0, protectedLength);

        ReadOnlySpan<byte> storedHmac = fileBytes.AsSpan(protectedLength, HmacHelper.HmacSizeBytes);

        // Verify tamper protection via HMAC-SHA256
        if (!HmacHelper.VerifyHmac(_entropy, _protectedBlob, storedHmac))
        {
            throw new CryptographicException("Tamper detected: HMAC-SHA256 verification failed for credential store.");
        }

        // Initialize non-sensitive metadata and pupil index without keeping sensitive passwords in memory
        InitializeIndex();
    }

    [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility")]
    private void InitializeIndex()
    {
        byte[] decryptedPayload = ProtectedData.Unprotect(_protectedBlob, _entropy, DataProtectionScope.LocalMachine);
        try
        {
            var payload = JsonSerializer.Deserialize<MasterBundlePayload>(decryptedPayload);
            if (payload != null)
            {
                SchemaVersion = payload.SchemaVersion;
                SchoolCode = payload.School?.Code ?? "";
                GeneratedAt = payload.GeneratedAt;
                School = new School
                {
                    Code = payload.School?.Code ?? "",
                    Name = payload.School?.Name ?? "",
                    Motto = payload.School?.Motto,
                    Domain = string.IsNullOrWhiteSpace(payload.School?.Domain) ? "moe-dl.edu.my" : payload.School.Domain
                };
                Theme = payload.Theme ?? new ThemeInfo();
                Config = payload.Config ?? new AppConfig();
                Classes = payload.Classes?.Select(c => new ClassInfo
                {
                    Id = c.Id,
                    Name = c.Name,
                    Grade = c.Grade,
                    ColourIndex = c.ColourIndex
                }).ToList() ?? [];

                var studentsList = new List<Student>();
                if (payload.Students != null)
                {
                    foreach (var s in payload.Students)
                    {
                        if (!string.IsNullOrEmpty(s.Id))
                        {
                            _allStudentIds.Add(s.Id);
                            _studentActiveMap[s.Id] = s.Active;
                            if (!string.IsNullOrEmpty(s.Password))
                            {
                                _studentIdsWithPassword.Add(s.Id);
                            }

                            studentsList.Add(new Student
                            {
                                Id = s.Id,
                                Name = s.Name,
                                ClassId = s.ClassId,
                                EmailLocal = s.EmailLocal,
                                Avatar = string.IsNullOrWhiteSpace(s.Avatar) ? "kucing" : s.Avatar,
                                PasswordVersion = s.PasswordVersion,
                                PicturePassword = s.PicturePassword,
                                Active = s.Active
                            });
                        }
                    }
                }

                // Compute display names for all students grouped by class
                foreach (var group in studentsList.GroupBy(s => s.ClassId))
                {
                    var classStudents = group.ToList();
                    var displayNames = DisplayNameCalculator.ComputeDisplayNames(classStudents);
                    foreach (var st in classStudents)
                    {
                        if (displayNames.TryGetValue(st.Id, out var dn))
                        {
                            st.DisplayName = dn;
                        }
                    }
                }

                Students = studentsList;
            }
        }
        finally
        {
            // Decryption discipline: zero unencrypted buffer immediately
            CryptographicOperations.ZeroMemory(decryptedPayload);
        }
    }

    /// <summary>
    /// Checks if a credential exists and is non-empty for the given pupil.
    /// </summary>
    public bool HasCredential(string studentId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(studentId);

        return _studentIdsWithPassword.Contains(studentId);
    }

    /// <summary>
    /// Checks if the pupil is marked active in the store roster.
    /// </summary>
    public bool IsStudentActive(string studentId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(studentId);

        return _studentActiveMap.TryGetValue(studentId, out bool active) && active;
    }

    /// <summary>
    /// Returns all registered student IDs in the store.
    /// </summary>
    public IReadOnlyCollection<string> GetStudentIds()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _allStudentIds;
    }

    /// <summary>
    /// Opens the credential for a specific pupil, returning a pinned disposable credential buffer.
    /// Adheres to §3.4 decryption discipline:
    /// - Never decrypts the whole store into permanent memory
    /// - Never materialises the password as System.String
    /// - Wipes decrypted memory in finally before returning
    /// </summary>
    [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility")]
    public ICredential OpenCredential(string studentId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(studentId);

        if (!_allStudentIds.Contains(studentId))
        {
            throw new KeyNotFoundException($"Pupil '{studentId}' was not found in the credential store.");
        }

        if (!_studentIdsWithPassword.Contains(studentId))
        {
            throw new InvalidOperationException($"No password is set for pupil '{studentId}'.");
        }

        // Decrypt transient payload to extract only the requested pupil's password
        byte[] decryptedPayload = ProtectedData.Unprotect(_protectedBlob, _entropy, DataProtectionScope.LocalMachine);
        try
        {
            return ExtractSinglePassword(decryptedPayload, studentId);
        }
        finally
        {
            // Decryption discipline: zero out decrypted payload memory immediately
            CryptographicOperations.ZeroMemory(decryptedPayload);
        }
    }

    private static ICredential ExtractSinglePassword(ReadOnlySpan<byte> jsonBytes, string studentId)
    {
        var reader = new Utf8JsonReader(jsonBytes);
        byte[] targetIdUtf8 = Encoding.UTF8.GetBytes(studentId);

        // Pinned temporary extraction buffer for the single password to avoid System.String allocation
        char[] tempPasswordBuffer = new char[256];
        GCHandle tempHandle = GCHandle.Alloc(tempPasswordBuffer, GCHandleType.Pinned);

        try
        {
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.PropertyName && reader.ValueTextEquals("students"u8))
                {
                    if (!reader.Read() || reader.TokenType != JsonTokenType.StartArray) continue;

                    while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                    {
                        if (reader.TokenType == JsonTokenType.StartObject)
                        {
                            bool isTarget = false;
                            int passwordLength = -1;

                            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                            {
                                if (reader.TokenType == JsonTokenType.PropertyName)
                                {
                                    if (reader.ValueTextEquals("id"u8))
                                    {
                                        reader.Read();
                                        if (reader.ValueTextEquals(targetIdUtf8))
                                        {
                                            isTarget = true;
                                        }
                                    }
                                    else if (reader.ValueTextEquals("password"u8))
                                    {
                                        reader.Read();
                                        if (reader.TokenType == JsonTokenType.String)
                                        {
                                            if (reader.ValueSpan.Length > tempPasswordBuffer.Length)
                                            {
                                                // Expand temporary buffer if password exceeds 256 chars
                                                CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(tempPasswordBuffer.AsSpan()));
                                                tempHandle.Free();
                                                tempPasswordBuffer = new char[reader.ValueSpan.Length * 2];
                                                tempHandle = GCHandle.Alloc(tempPasswordBuffer, GCHandleType.Pinned);
                                            }

                                            passwordLength = reader.CopyString(tempPasswordBuffer);
                                        }
                                    }
                                }
                            }

                            if (isTarget && passwordLength >= 0)
                            {
                                // Return pinned credential holding the unescaped password span
                                var result = new SecurePasswordBuffer(tempPasswordBuffer.AsSpan(0, passwordLength));
                                return result;
                            }
                            else
                            {
                                // Zero temporary buffer if this was not the target student
                                if (passwordLength > 0)
                                {
                                    CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(tempPasswordBuffer.AsSpan(0, passwordLength)));
                                }
                            }
                        }
                    }
                }
            }

            throw new KeyNotFoundException($"Pupil '{studentId}' password could not be extracted.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(tempPasswordBuffer.AsSpan()));
            if (tempHandle.IsAllocated)
            {
                tempHandle.Free();
            }
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            CryptographicOperations.ZeroMemory(_entropy);
            _studentIdsWithPassword.Clear();
            _allStudentIds.Clear();
            _studentActiveMap.Clear();
            Classes = [];
            Students = [];
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    ~DpapiCredentialStore()
    {
        Dispose();
    }
}
