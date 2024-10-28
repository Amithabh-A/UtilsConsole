namespace Updater;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Diagnostics;
using Networking.Serialization;



/*
 * ----------------- FileMetadataGenerator -----------------
 */

public class DirectoryMetadataGenerator
{

    private List<FileMetadata>? _metadata;

    /// <summary>
    /// Create metadata of directory
    /// </summary>
    /// <param name="directoryPath">Path of the directory</param>
    public DirectoryMetadataGenerator(string directoryPath = @"C:\Temp")
    {
        if (!Directory.Exists(directoryPath))
        {
            Debug.WriteLine($"Directory does not exist: {directoryPath}");
            Directory.CreateDirectory(directoryPath);
        }

        _metadata = CreateFileMetadata(directoryPath);
    }


    /// <summary>
    /// Get metadata
    /// </summary>
    /// <returns>List of FileMetadata objects. </returns>
    public List<FileMetadata>? GetMetadata()
    {
        return _metadata;
    }
    /// <summary> Return metadata of the specified directory
    /// </summary>
    /// <param name="directoryPath">Path of directory.</param>
    /// <param name="writeToFile">bool value to write metadata to file.</param>
    /// <returns>List of FileMetadata objects in the directory.</returns>
    private static List<FileMetadata> CreateFileMetadata(string directoryPath = @"C:\Temp")
    {
        List<FileMetadata> metadata = new List<FileMetadata>();

        foreach (string filePath in Directory.GetFiles(directoryPath))
        {

            metadata.Add(new FileMetadata
            {
                FileName = Path.GetFileName(filePath),
                FileHash = ComputeFileHash(filePath)
            });
        }

        return metadata;
    }

    /// <summary>
    /// Computes SHA-256 hash of file. 
    /// </summary>
    /// <param name="filePath">Path of file</param>
    /// <returns>SHA-256 hash of file</returns>
    private static string ComputeFileHash(string filePath)
    {
        using SHA256 sha256 = SHA256.Create();
        using FileStream stream = File.OpenRead(filePath);
        Byte[] hashBytes = sha256.ComputeHash(stream);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }
}


/*
 * ----------------- MetadataComparer -----------------
 */


public class DirectoryMetadataComparer
{
    private Dictionary<int, List<object>>? _differences;
    private List<string> _uniqueServerFiles = new List<string>();
    private List<string> _uniqueClientFiles = new List<string>();


    /// <summary>
    /// Initialize new instance. 
    /// </summary>
    /// <param name="metadataA">Dir. A's metadata</param>
    /// <param name="metadataB">Dir. B's metadata</param>
    public DirectoryMetadataComparer(List<FileMetadata> metadataA, List<FileMetadata> metadataB)
    {
        _differences = CompareMetadata(metadataA, metadataB);
    }

    public List<string> GetUniqueServerFiles()
    {
        return _uniqueServerFiles;
    }


    public List<string> GetUniqueClientFiles()
    {
        return _uniqueServerFiles;
    }

    /// <summary>
    /// Get _differences
    /// </summary>
    /// <returns>Dictionary containing differences between metadata of dir. A and B</returns>
    public Dictionary<int, List<object>>? GetDifferences()
    {
        return _differences;
    }


    /// <summary>
    /// Compares and generate difference between a dir. metadata pair
    /// </summary>
    /// <param name="metadataA">Dir. A's metadata</param>
    /// <param name="metadataB">Dir. B's metadata</param>
    /// <returns>Dictionary containing differences, </returns>
    private Dictionary<int, List<object>> CompareMetadata(List<FileMetadata> metadataA, List<FileMetadata> metadataB)
    {
        Dictionary<int, List<object>> differences = new Dictionary<int, List<object>>
        {
            { -1, new List<object>() }, // In B but not in A
            { 0, new List<object>() },  // Files with same hash but different names
            { 1, new List<object>() }   // In A but not in B
        };

        Dictionary<string, string> hashToFileA = CreateHashToFileDictionary(metadataA);
        Dictionary<string, string> hashToFileB = CreateHashToFileDictionary(metadataB);

        CheckForRenamesAndMissingFiles(metadataB, hashToFileA, differences);
        CheckForOnlyInAFiles(metadataA, hashToFileB, differences);

        return differences;
    }


    /// <summary>
    /// Create a map from filehash to filename
    /// </summary>
    /// <param name="metadata">list of metadata.</param>
    /// <returns>Dictionary containing mapping.</returns>
    private static Dictionary<string, string> CreateHashToFileDictionary(List<FileMetadata> metadata)
    {
        var hashToFile = new Dictionary<string, string>();
        foreach (var file in metadata)
        {
            hashToFile[file.FileHash] = file.FileName;
        }
        return hashToFile;
    }


    /// <summary>
    /// Checks for files in directory B that have been renamed or missing in directory A.
    /// </summary>
    /// <param name="metadataB">Dir. B's metadata.</param>
    /// <param name="hashToFileA">Dir. A's Hash to file map</param>
    /// <param name="differences">differences dictionary</param>
    /// <returns> Differences dictionary</returns>
    private void CheckForRenamesAndMissingFiles(List<FileMetadata> metadataB, Dictionary<string, string> hashToFileA, Dictionary<int, List<object>> differences)
    {
        foreach (FileMetadata fileB in metadataB)
        {
            if (hashToFileA.ContainsKey(fileB.FileHash))
            {
                if (hashToFileA[fileB.FileHash] != fileB.FileName)
                {
                    differences[0].Add(new Dictionary<string, string>
                    {
                        { "RenameFrom", fileB.FileName },
                        { "RenameTo", hashToFileA[fileB.FileHash] },
                        { "FileHash", fileB.FileHash }
                    });
                }
            }
            else
            {
                differences[-1].Add(new Dictionary<string, string>
                {
                    { "FileName", fileB.FileName },
                    { "FileHash", fileB.FileHash }
                });
                _uniqueClientFiles.Add(fileB.FileName);
            }
        }
    }


    /// <summary>
    /// Checks for files in directory A that are missing in directory B.
    /// </summary>
    /// <param name="metadataA">Dir. A's metadata</param>
    /// <param name="hashToFileB">Dir. B's Hash to file map</param>
    /// <param name="differences">Differences dictionary</param>
    /// <returns> Differences dictionary</returns>
    private void CheckForOnlyInAFiles(List<FileMetadata> metadataA, Dictionary<string, string> hashToFileB, Dictionary<int, List<object>> differences)
    {
        foreach (FileMetadata fileA in metadataA)
        {
            if (!hashToFileB.ContainsKey(fileA.FileHash))
            {
                differences[1].Add(new Dictionary<string, string>
                {
                    { "FileName", fileA.FileName },
                    { "FileHash", fileA.FileHash }
                });
                _uniqueServerFiles.Add(fileA.FileName);
            }
        }
    }
}



public class Utils
{

    /// <summary>
    /// Reads the content of the specified file.
    /// </summary>
    /// <param name="filePath">Path of file to read. </param>
    /// <returns>Filecontent as string, or null if file dne</returns>
    public static string? ReadFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Debug.WriteLine("File not found. Please check the path and try again.");
            return null;
        }

        return File.ReadAllText(filePath);

    }


    /// <summary>
    /// Write/Overwrite content to file
    /// </summary>
    /// <param name="filePath">Path of file</param>
    /// <param name="content">Content to write.</param>
    public static bool WriteToFile(string filePath, string content)
    {
        try
        {
            File.WriteAllText(filePath, content);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"An error occurred while writing to the file: {ex.Message}");
            return false;
        }
    }


    /// <summary> Serializes an object to its string representation.
    /// </summary>
    /// <typeparam name="T">The type of the object to serialize.</typeparam>
    /// <param name="obj">The object to serialize.</param>
    /// <returns>A string representation of the serialized object.</returns>
    public static string SerializeObject<T>(T obj)
    {
        ISerializer serializer = new Serializer();
        return serializer.Serialize(obj);
    }


    /// <summary>
    /// Deserializes a string back to an object of specified type.
    /// </summary>
    /// <typeparam name="T">The type of the object to deserialize into.</typeparam>
    /// <param name="serializedData">The serialized string data.</param>
    /// <returns>An instance of the specified type.</returns>
    public static T DeserializeObject<T>(string serializedData)
    {
        ISerializer serializer = new Serializer();
        return serializer.Deserialize<T>(serializedData);
    }




    /*
     * ----------------- Application Level Implementations -----------------
     */

    /// <summary>
    /// Generates serialized packet containing metadata of files in a directory.
    /// </summary>
    /// <returns>Serialized packet containing metadata of files in a directory.</returns>
    public static string SerializedMetadataPacket()
    {
        DirectoryMetadataGenerator metadataGenerator = new DirectoryMetadataGenerator();

        if (metadataGenerator == null)
        {
            throw new Exception("Failed to create DirectoryMetadataGenerator");
        }

        List<FileMetadata>? metadata = metadataGenerator.GetMetadata();
        if (metadata == null)
        {
            throw new Exception("Failed to get metadata");
        }

        string serializedMetadata = Utils.SerializeObject(metadata);
        FileContent fileContent = new FileContent("metadata.json", serializedMetadata);
        List<FileContent> fileContents = new List<FileContent> { fileContent };

        DataPacket dataPacket = new DataPacket(DataPacket.PacketType.Metadata, fileContents);
        return SerializeObject(dataPacket);

    }





}
