using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System.IO;
using System.Text;
using System.Net;
using System.Threading;
using Newtonsoft.Json;

public class RhubarbLipSyncManager : MonoBehaviour
{
    [Header("HTTP Server Configuration")]
    [SerializeField] private int port = 8080;
    [SerializeField] private string uploadEndpoint = "/avatar/upload";
    [SerializeField] private string speakEndpoint = "/avatar/speak";
    
    [Header("Audio Source Configuration")]
    [SerializeField] private AudioSource audioSource;
    
    [Header("File Storage Configuration")]
    [SerializeField] private string storageFolder = "LipsyncData";
    [SerializeField] private StorageLocation storageLocation = StorageLocation.StreamingAssetsPath;
    [SerializeField] private bool deleteAfterPlaying = false;
    
    public enum StorageLocation
    {
        PersistentDataPath,
        DataPath,
        TemporaryCachePath,
        StreamingAssetsPath,
        CustomPath
    }
    
    [SerializeField] private string customStoragePath = "";
    
    private ExpressionController expressionController;
    private AnimationScheduler animationScheduler;
    private HttpListener httpListener;
    private string dataPath;
    private Thread listenerThread;
    private Queue<HttpListenerContext> pendingRequests = new Queue<HttpListenerContext>();
    private bool isRunning = false;
    
    private void Awake()
    {
        // Ottieni i componenti necessari
        expressionController = GetComponent<ExpressionController>();
        animationScheduler = expressionController.GetScheduler();
        
        // Determina il percorso di archiviazione in base alla configurazione
        switch (storageLocation)
        {
            case StorageLocation.PersistentDataPath:
                dataPath = Path.Combine(Application.persistentDataPath, storageFolder);
                break;
            case StorageLocation.DataPath:
                dataPath = Path.Combine(Application.dataPath, storageFolder);
                break;
            case StorageLocation.TemporaryCachePath:
                dataPath = Path.Combine(Application.temporaryCachePath, storageFolder);
                break;
            case StorageLocation.StreamingAssetsPath:
                dataPath = Path.Combine(Application.streamingAssetsPath, storageFolder);
                break;
            case StorageLocation.CustomPath:
                dataPath = string.IsNullOrEmpty(customStoragePath) 
                    ? Path.Combine(Application.persistentDataPath, storageFolder) 
                    : Path.Combine(customStoragePath, storageFolder);
                break;
        }
        
        Debug.Log($"Files will be stored at: {dataPath}");
        
        // Crea il percorso di archiviazione
        if (!Directory.Exists(dataPath))
        {
            try 
            {
                Directory.CreateDirectory(dataPath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to create storage directory: {ex.Message}");
                // Fallback to temporary cache path
                dataPath = Path.Combine(Application.temporaryCachePath, storageFolder);
                Directory.CreateDirectory(dataPath);
                Debug.Log($"Using fallback path: {dataPath}");
            }
        }
        
        // Avvia il server HTTP
        StartHttpServer();
    }
    
    private void Update()
    {
        // Processa le richieste in coda nel thread principale
        lock (pendingRequests)
        {
            if (pendingRequests.Count > 0)
            {
                HttpListenerContext context = pendingRequests.Dequeue();
                StartCoroutine(ProcessRequestInMainThread(context));
            }
        }
    }
    
    private void StartHttpServer()
    {
        try
        {
            isRunning = true;
            
            httpListener = new HttpListener();
            httpListener.Prefixes.Add($"http://localhost:{port}/");
            httpListener.Start();
            
            Debug.Log($"Lip sync server started on port {port}");
            
            // Avvia il thread di ascolto
            listenerThread = new Thread(ListenerThreadMethod);
            listenerThread.IsBackground = true;
            listenerThread.Start();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to start HTTP server: {ex.Message}");
            isRunning = false;
        }
    }
    
    private void ListenerThreadMethod()
    {
        while (isRunning && httpListener != null && httpListener.IsListening)
        {
            try
            {
                // Attendi una richiesta
                HttpListenerContext context = httpListener.GetContext();
                
                // Metti la richiesta in coda per elaborarla nel thread principale
                lock (pendingRequests)
                {
                    pendingRequests.Enqueue(context);
                }
            }
            catch (Exception ex)
            {
                if (isRunning)
                {
                    Debug.LogError($"Error in HTTP listener thread: {ex.Message}");
                }
            }
        }
    }
    
    private IEnumerator ProcessRequestInMainThread(HttpListenerContext context)
    {
        string url = context.Request.Url.AbsolutePath;
        
        if (url.Equals(uploadEndpoint, StringComparison.OrdinalIgnoreCase) && 
            context.Request.HttpMethod == "POST")
        {
            yield return ProcessUploadRequest(context);
        }
        else if (url.Equals(speakEndpoint, StringComparison.OrdinalIgnoreCase))
        {
            string fileName = context.Request.QueryString["file"];
            if (string.IsNullOrEmpty(fileName))
            {
                SendErrorResponse(context.Response, "Missing 'file' parameter");
            }
            else
            {
                yield return ProcessSpeakRequest(fileName, context.Response);
            }
        }
        else
        {
            // Endpoint non supportato
            context.Response.StatusCode = 404;
            context.Response.Close();
        }
    }
    
    private IEnumerator ProcessUploadRequest(HttpListenerContext context)
    {
        try
        {
            HttpListenerRequest request = context.Request;
            
            // Verifica che sia un form multipart
            if (!request.ContentType.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase))
            {
                SendErrorResponse(context.Response, "Expected multipart/form-data");
                yield break;
            }
            
            // Ottieni il boundary del multipart
            string boundary = GetBoundary(request.ContentType);
            if (string.IsNullOrEmpty(boundary))
            {
                SendErrorResponse(context.Response, "Invalid multipart/form-data boundary");
                yield break;
            }
            
            // Ottieni il tipo di file e il nome dal form
            string fileName = null;
            string fileType = null;
            string tempFilePath = null;
            
            using (var ms = new MemoryStream())
            {
                // Copia l'input stream nella memory stream
                request.InputStream.CopyTo(ms);
                byte[] requestData = ms.ToArray();
                
                // Estrai le parti del multipart
                MultipartFormDataParser parser = new MultipartFormDataParser(requestData, boundary);
                
                // Ottieni informazioni dal form
                fileName = parser.GetFormValue("fileName");
                fileType = parser.GetFormValue("fileType");
                
                if (string.IsNullOrEmpty(fileName) || string.IsNullOrEmpty(fileType))
                {
                    SendErrorResponse(context.Response, "Missing fileName or fileType parameter");
                    yield break;
                }
                
                if (fileType != "audio" && fileType != "lipsync")
                {
                    SendErrorResponse(context.Response, "fileType must be 'audio' or 'lipsync'");
                    yield break;
                }
                
                // Ottieni il file
                byte[] fileData = parser.GetFileData("file");
                if (fileData == null || fileData.Length == 0)
                {
                    SendErrorResponse(context.Response, "No file data found");
                    yield break;
                }
                
                // Determina l'estensione del file in base al tipo
                string extension = fileType.ToLower() == "audio" ? ".mp3" : ".json";
                
                // Salva il file
                string filePath = Path.Combine(dataPath, $"{fileName}{extension}");
                Debug.Log($"Tentativo di salvare file in: {filePath} (Dimensione: {fileData.Length} bytes)");
                
                File.WriteAllBytes(filePath, fileData);
                
                // Invia una risposta di successo
                SendSuccessResponse(context.Response, $"File {fileName}{extension} uploaded successfully");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error processing upload request: {ex.Message}");
            SendErrorResponse(context.Response, ex.Message);
        }
    }
    
    private string GetBoundary(string contentType)
    {
        int index = contentType.IndexOf("boundary=");
        if (index == -1)
            return null;
        
        return contentType.Substring(index + 9); // 9 è la lunghezza di "boundary="
    }
    
    private IEnumerator ProcessSpeakRequest(string fileName, HttpListenerResponse response)
    {
        // Verifica che i file esistano
        string audioFilePath = Path.Combine(dataPath, $"{fileName}.mp3");
        string jsonFilePath = Path.Combine(dataPath, $"{fileName}.json");
        
        if (!File.Exists(audioFilePath))
        {
            SendErrorResponse(response, $"Audio file for {fileName} not found");
            yield break;
        }
        
        if (!File.Exists(jsonFilePath))
        {
            SendErrorResponse(response, $"Lipsync file for {fileName} not found");
            yield break;
        }
        
        // Carica l'audio - fuori dal blocco try per il yield return
        string fileUrl = "file://" + audioFilePath;
        AudioClip audioClip = null;
        UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(fileUrl, AudioType.MPEG);
        
        // Questo yield deve stare fuori dal blocco try-catch
        yield return www.SendWebRequest();
        
        try
        {
            if (www.result == UnityWebRequest.Result.Success)
            {
                audioClip = DownloadHandlerAudioClip.GetContent(www);
            }
            else
            {
                Debug.LogError($"Failed to load audio: {www.error}");
                SendErrorResponse(response, $"Failed to load audio: {www.error}");
                yield break;
            }
            
            // Carica i dati di lipsync
            string lipsyncJson = File.ReadAllText(jsonFilePath);

            Debug.Log("aggiungendo lipsync");
            
            // Crea l'animazione di sincronizzazione labiale
            RhubarbLipSyncAnimation lipSyncAnimation = new RhubarbLipSyncAnimation(audioClip, lipsyncJson);
            
            // Aggiungi l'animazione allo scheduler
            animationScheduler.EnqueueAnimation(lipSyncAnimation);
            
            // Invia una risposta di successo
            SendSuccessResponse(response, $"Playing {fileName}");
            
            // Se l'opzione è attivata, elimina i file dopo la riproduzione
            if (deleteAfterPlaying)
            {
                // Aspetta che l'audio finisca prima di eliminare i file
                // (approssimato usando la lunghezza dell'audio + margine di sicurezza)
                float duration = audioClip.length + 1.0f;
                StartCoroutine(DeleteFilesAfterDelay(audioFilePath, jsonFilePath, duration));
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error processing speak request: {ex.Message}");
            SendErrorResponse(response, ex.Message);
        }
        finally
        {
            www.Dispose();
        }
    }
    
    private IEnumerator DeleteFilesAfterDelay(string audioFilePath, string jsonFilePath, float delay)
    {
        yield return new WaitForSeconds(delay);
        
        try
        {
            if (File.Exists(audioFilePath))
                File.Delete(audioFilePath);
                
            if (File.Exists(jsonFilePath))
                File.Delete(jsonFilePath);
                
            Debug.Log($"Files deleted after playback");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error deleting files: {ex.Message}");
        }
    }
    
    private void SendSuccessResponse(HttpListenerResponse response, string message)
    {
        string responseJson = $"{{\"status\":\"success\",\"message\":\"{message}\"}}";
        byte[] buffer = Encoding.UTF8.GetBytes(responseJson);
        
        response.ContentLength64 = buffer.Length;
        response.ContentType = "application/json";
        response.OutputStream.Write(buffer, 0, buffer.Length);
        response.Close();
    }
    
    private void SendErrorResponse(HttpListenerResponse response, string errorMessage)
    {
        string responseJson = $"{{\"status\":\"error\",\"message\":\"{errorMessage}\"}}";
        byte[] buffer = Encoding.UTF8.GetBytes(responseJson);
        
        response.ContentLength64 = buffer.Length;
        response.ContentType = "application/json";
        response.OutputStream.Write(buffer, 0, buffer.Length);
        response.Close();
    }
    
    private void OnDestroy()
    {
        isRunning = false;
        
        if (httpListener != null)
        {
            httpListener.Stop();
            httpListener.Close();
            httpListener = null;
        }
        
        if (listenerThread != null && listenerThread.IsAlive)
        {
            listenerThread.Join(500); // Attendi che il thread termini
        }
    }
    
    // Classe helper per il parsing di multipart/form-data
    private class MultipartFormDataParser
    {
        private byte[] data;
        private string boundary;
        private Dictionary<string, string> formValues = new Dictionary<string, string>();
        private Dictionary<string, byte[]> fileData = new Dictionary<string, byte[]>();
        
        public MultipartFormDataParser(byte[] data, string boundary)
        {
            this.data = data;
            this.boundary = boundary;
            Parse();
        }
        
        private void Parse()
        {
            // Questa è una implementazione semplificata
            // In un'implementazione reale, dovremmo analizzare correttamente tutti i byte
            // Qui assumiamo una struttura specifica per semplicità
            
            string dataStr = Encoding.UTF8.GetString(data);
            string[] parts = dataStr.Split(new[] { "--" + boundary }, StringSplitOptions.None);
            
            foreach (string part in parts)
            {
                if (string.IsNullOrEmpty(part) || part.Trim() == "--")
                    continue;
                
                // Separa gli headers dal contenuto
                int headerEndIndex = part.IndexOf("\r\n\r\n");
                if (headerEndIndex == -1)
                    continue;
                
                string headers = part.Substring(0, headerEndIndex);
                string content = part.Substring(headerEndIndex + 4);
                
                // Trova il nome del campo
                int nameStartIndex = headers.IndexOf("name=\"");
                if (nameStartIndex == -1)
                    continue;
                
                nameStartIndex += 6;
                int nameEndIndex = headers.IndexOf("\"", nameStartIndex);
                string name = headers.Substring(nameStartIndex, nameEndIndex - nameStartIndex);
                
                // Controlla se è un file
                bool isFile = headers.Contains("filename=\"");
                
                if (isFile)
                {
                    // Nota: in una implementazione reale, dovremmo determinare l'inizio e la fine dei dati binari
                    // con più precisione. Qui assumiamo che il contenuto binario inizi dopo gli headers.
                    byte[] fileDataBytes = Encoding.UTF8.GetBytes(content);
                    fileData[name] = fileDataBytes;
                }
                else
                {
                    // Rimuovi eventuali CRLF alla fine
                    if (content.EndsWith("\r\n"))
                        content = content.Substring(0, content.Length - 2);
                    
                    formValues[name] = content;
                }
            }
        }
        
        public string GetFormValue(string name)
        {
            return formValues.TryGetValue(name, out string value) ? value : null;
        }
        
        public byte[] GetFileData(string name)
        {
            return fileData.TryGetValue(name, out byte[] value) ? value : null;
        }
    }
}