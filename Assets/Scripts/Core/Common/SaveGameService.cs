using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json;

namespace MarbleMaker.Core
{
    /// <summary>
    /// Save/Load service implementing the exact specifications from TDD Section 4 & 5
    /// Format: JSON.gz with schema versioning and atomic writes
    /// </summary>
    [CreateAssetMenu(fileName = "SaveGameService", menuName = "MarbleMaker/Save Game Service")]
    public class SaveGameService : ScriptableObject
    {
        [Header("Save Settings (from TDD Section 5)")]
        [SerializeField] [Tooltip("Maximum save file size in KB (from TDD Section 6)")]
        private int maxSaveSize = GameConstants.MAX_SAVE_SIZE_KB;
        
        [SerializeField] [Tooltip("Enable compression (JSON.gz format from TDD)")]
        private bool enableCompression = true;
        
        [SerializeField] [Tooltip("Current save format version")]
        private int currentVersion = 1;
        
        [Header("File Paths")]
        [SerializeField] [Tooltip("Save file name")]
        private string saveFileName = "savegame.json.gz";
        
        [SerializeField] [Tooltip("Profile file name")]
        private string profileFileName = "profile.dat";
        
        [SerializeField] [Tooltip("Backup file extension")]
        private string backupExtension = ".bak";
        
        [Header("Debug Options")]
        [SerializeField] [Tooltip("Enable debug logging")]
        private bool enableDebugLogging = false;
        
        [SerializeField] [Tooltip("Pretty print JSON for debugging")]
        private bool prettyPrintJson = false;
        
        /// <summary>
        /// Current save format version
        /// </summary>
        public int CurrentVersion => currentVersion;
        
        /// <summary>
        /// Maximum save size in KB
        /// </summary>
        public int MaxSaveSize => maxSaveSize;
        
        /// <summary>
        /// Save path for persistent data
        /// </summary>
        public string SavePath => Application.persistentDataPath;
        
        /// <summary>
        /// Full path to save file
        /// </summary>
        public string SaveFilePath => Path.Combine(SavePath, saveFileName);
        
        /// <summary>
        /// Full path to profile file
        /// </summary>
        public string ProfileFilePath => Path.Combine(SavePath, profileFileName);
        
        /// <summary>
        /// JSON serializer settings for consistency
        /// </summary>
        private JsonSerializerSettings jsonSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore,
            DateTimeZoneHandling = DateTimeZoneHandling.Utc,
            Formatting = Formatting.None // Will be set based on prettyPrintJson
        };
        
        /// <summary>
        /// Registered save migrators for version upgrades
        /// </summary>
        private readonly Dictionary<int, ISaveMigrator> migrators = new Dictionary<int, ISaveMigrator>();
        
        private void OnValidate()
        {
            // Update JSON formatting based on debug setting
            if (jsonSettings != null)
            {
                jsonSettings.Formatting = prettyPrintJson ? Formatting.Indented : Formatting.None;
            }
        }
        
        /// <summary>
        /// Registers a save migrator for version upgrades
        /// </summary>
        /// <param name="migrator">Migrator to register</param>
        public void RegisterMigrator(ISaveMigrator migrator)
        {
            migrators[migrator.FromVersion] = migrator;
        }
        
        /// <summary>
        /// Saves board and profile data asynchronously
        /// Implements atomic writes via temp-file + rename from TDD Section 5
        /// </summary>
        /// <param name="board">Board data to save</param>
        /// <param name="profile">Profile data to save</param>
        /// <returns>Task representing the save operation</returns>
        public async Task SaveAsync(BoardData board, ProfileData profile)
        {
            try
            {
                // Create save data structure
                var saveData = new SaveData
                {
                    version = currentVersion,
                    board = board,
                    profile = profile,
                    saveTime = DateTime.UtcNow
                };
                
                // Serialize to JSON
                jsonSettings.Formatting = prettyPrintJson ? Formatting.Indented : Formatting.None;
                string json = JsonConvert.SerializeObject(saveData, jsonSettings);
                
                // Validate size before compression
                int jsonSize = Encoding.UTF8.GetByteCount(json);
                if (enableDebugLogging)
                {
                    Debug.Log($"SaveGameService: JSON size before compression: {jsonSize / 1024}KB");
                }
                
                // Compress to JSON.gz format if enabled
                byte[] data;
                if (enableCompression)
                {
                    data = await CompressJsonAsync(json);
                    
                    if (enableDebugLogging)
                    {
                        Debug.Log($"SaveGameService: Compressed size: {data.Length / 1024}KB");
                    }
                }
                else
                {
                    data = Encoding.UTF8.GetBytes(json);
                }
                
                // Validate compressed size
                if (data.Length > maxSaveSize * 1024)
                {
                    throw new InvalidOperationException($"Save file too large: {data.Length / 1024}KB > {maxSaveSize}KB");
                }
                
                // Atomic write: write to temp file, then rename
                await WriteFileAtomicallyAsync(SaveFilePath, data);
                
                if (enableDebugLogging)
                {
                    Debug.Log($"SaveGameService: Save completed successfully to {SaveFilePath}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"SaveGameService: Save failed: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Loads board and profile data asynchronously
        /// Implements version migration from TDD Section 5
        /// </summary>
        /// <returns>Loaded save data, or null if no save exists</returns>
        public async Task<SaveData> LoadAsync()
        {
            try
            {
                if (!File.Exists(SaveFilePath))
                {
                    if (enableDebugLogging)
                    {
                        Debug.Log("SaveGameService: No save file found, returning null");
                    }
                    return null;
                }
                
                // Read file data
                byte[] data = await File.ReadAllBytesAsync(SaveFilePath);
                
                // Decompress if needed
                string json;
                if (enableCompression)
                {
                    json = await DecompressJsonAsync(data);
                }
                else
                {
                    json = Encoding.UTF8.GetString(data);
                }
                
                // Deserialize JSON
                var saveData = JsonConvert.DeserializeObject<SaveData>(json, jsonSettings);
                
                if (saveData == null)
                {
                    throw new InvalidOperationException("Failed to deserialize save data");
                }
                
                // Perform version migration if needed
                saveData = await MigrateVersionAsync(saveData);
                
                if (enableDebugLogging)
                {
                    Debug.Log($"SaveGameService: Load completed successfully from {SaveFilePath}");
                }
                
                return saveData;
            }
            catch (Exception ex)
            {
                Debug.LogError($"SaveGameService: Load failed: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Compresses JSON string to gzip format
        /// </summary>
        /// <param name="json">JSON string to compress</param>
        /// <returns>Compressed byte array</returns>
        private async Task<byte[]> CompressJsonAsync(string json)
        {
            return await Task.Run(() =>
            {
                using (var memoryStream = new MemoryStream())
                using (var gzipStream = new GZipStream(memoryStream, System.IO.Compression.CompressionLevel.Optimal))
                using (var writer = new StreamWriter(gzipStream, Encoding.UTF8))
                {
                    writer.Write(json);
                    writer.Flush();
                    gzipStream.Flush();
                    return memoryStream.ToArray();
                }
            });
        }
        
        /// <summary>
        /// Decompresses gzip data to JSON string
        /// </summary>
        /// <param name="data">Compressed byte array</param>
        /// <returns>Decompressed JSON string</returns>
        private async Task<string> DecompressJsonAsync(byte[] data)
        {
            return await Task.Run(() =>
            {
                using (var memoryStream = new MemoryStream(data))
                using (var gzipStream = new GZipStream(memoryStream, CompressionMode.Decompress))
                using (var reader = new StreamReader(gzipStream, Encoding.UTF8))
                {
                    return reader.ReadToEnd();
                }
            });
        }
        
        /// <summary>
        /// Writes file atomically using temp file + rename
        /// TDD Section 5: "Atomic writes via temp-file + rename"
        /// </summary>
        /// <param name="filePath">Target file path</param>
        /// <param name="data">Data to write</param>
        private async Task WriteFileAtomicallyAsync(string filePath, byte[] data)
        {
            string tempPath = filePath + ".tmp";
            string backupPath = filePath + backupExtension;
            
            try
            {
                // Write to temp file
                await File.WriteAllBytesAsync(tempPath, data);
                
                // Create backup of existing file if it exists
                if (File.Exists(filePath))
                {
                    if (File.Exists(backupPath))
                    {
                        File.Delete(backupPath);
                    }
                    File.Move(filePath, backupPath);
                }
                
                // Atomic rename
                File.Move(tempPath, filePath);
                
                if (enableDebugLogging)
                {
                    Debug.Log($"SaveGameService: Atomic write completed for {filePath}");
                }
            }
            catch (Exception ex)
            {
                // Cleanup temp file on failure
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
                
                // Restore backup if rename failed
                if (File.Exists(backupPath) && !File.Exists(filePath))
                {
                    File.Move(backupPath, filePath);
                }
                
                throw new InvalidOperationException($"Atomic write failed for {filePath}: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// Migrates save data through version upgrade chain
        /// TDD Section 5: "Version upgrade path – if loader sees an older schema, it calls a chain of Upgraders"
        /// </summary>
        /// <param name="saveData">Save data to migrate</param>
        /// <returns>Migrated save data</returns>
        private async Task<SaveData> MigrateVersionAsync(SaveData saveData)
        {
            if (saveData.version >= currentVersion)
            {
                return saveData; // No migration needed
            }
            
            return await Task.Run(() =>
            {
                var currentData = saveData;
                int version = saveData.version;
                
                // Apply migration chain
                while (version < currentVersion)
                {
                    if (migrators.TryGetValue(version, out ISaveMigrator migrator))
                    {
                        currentData = migrator.Migrate(currentData);
                        version = migrator.ToVersion;
                        
                        if (enableDebugLogging)
                        {
                            Debug.Log($"SaveGameService: Migrated from version {migrator.FromVersion} to {migrator.ToVersion}");
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException($"No migrator found for version {version}");
                    }
                }
                
                return currentData;
            });
        }
        
        /// <summary>
        /// Deletes save file
        /// </summary>
        public void DeleteSave()
        {
            try
            {
                if (File.Exists(SaveFilePath))
                {
                    File.Delete(SaveFilePath);
                    
                    if (enableDebugLogging)
                    {
                        Debug.Log($"SaveGameService: Save file deleted: {SaveFilePath}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"SaveGameService: Failed to delete save file: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Checks if a save file exists
        /// </summary>
        /// <returns>True if save file exists</returns>
        public bool SaveExists()
        {
            return File.Exists(SaveFilePath);
        }
        
        /// <summary>
        /// Gets save file size in KB
        /// </summary>
        /// <returns>Save file size in KB, or 0 if file doesn't exist</returns>
        public int GetSaveFileSizeKB()
        {
            if (!File.Exists(SaveFilePath))
            {
                return 0;
            }
            
            try
            {
                var fileInfo = new FileInfo(SaveFilePath);
                return (int)(fileInfo.Length / 1024);
            }
            catch (Exception ex)
            {
                Debug.LogError($"SaveGameService: Failed to get save file size: {ex.Message}");
                return 0;
            }
        }
    }
}