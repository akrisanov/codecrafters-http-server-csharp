using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;

Dictionary<int, string> httpStatusCodes = new()
{
    [200] = "200 OK",
    [201] = "201 Created",
    [404] = "404 Not Found",
};

var filesDir = GetFilesDir(args);

Console.WriteLine("Starting server...");
Console.WriteLine($"Serving files from {filesDir}");
Console.WriteLine("Press Ctrl+C to stop the server");

using TcpListener server = new(IPAddress.Any, 4221); // TcpListener is unmanaged resource
server.Start();

while (true)
{
    // handle each client in a separate task, fire and forget
    _ = HandleClientConnectionAsync();
}

async Task HandleClientConnectionAsync()
{
    // wait for a client to connect
    using var client = await server.AcceptSocketAsync(); // Socket is unmanaged resource

    while (client.Connected)
    {
        // receive request from the client
        var requestBytes = new byte[1024];
        var bytesRead = await client.ReceiveAsync(requestBytes);
        if (bytesRead == 0) break; // closed connection

        var request = Encoding.UTF8.GetString(requestBytes, 0, bytesRead);

        var requestMembers = request.Split("\r\n");

        // parse the request
        var requestTarget = requestMembers[0].Split(' ');
        var requestMethod = requestTarget[0]; // HTTP method
        var requestUrl = requestTarget[1]; // URL
        var urlPath = requestUrl.TrimStart('/').Split('/');
        var endpoint = urlPath[0]; // endpoint or page

        // build a response
        var status = httpStatusCodes[200];
        var headers = new Dictionary<string, object>
        {
            ["Connection"] = "keep-alive",
            ["Content-Type"] = "text/plain",
            ["Content-Length"] = 0,
        };
        var content = "";

        switch (endpoint)
        {
            case "": // root URL
                break;
            case "echo": // return URL parameter as the response content
                content = urlPath[1];
                headers["Content-Length"] = content.Length;
                break;
            case "user-agent": // return User-Agent header as the response content
                var userAgentHeader = requestMembers.FirstOrDefault(p => p.StartsWith("User-Agent"));
                var userAgent = userAgentHeader?.Split(": ")[1] ?? "";
                content = userAgent;
                headers["Content-Length"] = content.Length;
                break;
            case "files": // serve files from the specified directory --directory
                if (requestMethod == "GET")
                {
                    (status, var fileContent) = await ServeFileAsync(filesDir, urlPath[1]);
                    headers["Content-Type"] = "application/octet-stream";
                    headers["Content-Length"] = fileContent.Length;
                    content = Encoding.UTF8.GetString(fileContent);
                }
                else if (requestMethod == "POST")
                {
                    var contentLengthHeader = requestMembers.FirstOrDefault(p => p.StartsWith("Content-Length"));
                    var contentLength = int.TryParse(contentLengthHeader?.Split(": ")[1], out int result) ? result : 0;
                    var requestBody = requestMembers[^1];
                    status = await CreateFileAsync(filesDir, urlPath[1], requestBody);
                }
                else
                {
                    status = httpStatusCodes[404];
                }
                break;
            default:
                status = httpStatusCodes[404];
                break;
        }

        // support compression
        var acceptEncodingHeader = requestMembers.FirstOrDefault(p => p.StartsWith("Accept-Encoding"));
        string acceptEncoding = acceptEncodingHeader?.Split(": ")[1] ?? "";
        var compressionSchemes = acceptEncoding.Split(", ");

        if (compressionSchemes.Contains("gzip"))
        {
            headers["Content-Encoding"] = "gzip";
            content = CompressWithGzip(content);
        }

        // create HTTP response by combining status line, headers, and content

        var response = new StringBuilder();
        response.Append($"HTTP/1.1 {status}\r\n");
        foreach (var (key, value) in headers)
        {
            response.Append($"{key}: {value}\r\n");
        }
        response.Append("\r\n");
        response.Append(content);

        // send response to the client
        var responseBytes = Encoding.UTF8.GetBytes(response.ToString());
        await client.SendAsync(responseBytes);
    }
}

async Task<(string status, byte[] content)> ServeFileAsync(string dirName, string fileName)
{
    var filePath = Path.Combine(dirName, fileName);
    if (!File.Exists(filePath)) return (httpStatusCodes[404], []);
    var fileContent = await File.ReadAllBytesAsync(filePath);
    return (httpStatusCodes[200], fileContent);
}

async Task<string> CreateFileAsync(string dirName, string fileName, string content)
{
    var filePath = Path.Combine(dirName, fileName);
    if (File.Exists(filePath))
    {
        return httpStatusCodes[201];
    }
    await File.WriteAllTextAsync(filePath, content);
    return httpStatusCodes[201];
}

static string GetFilesDir(string[] args)
{
    var dirName = "/tmp";
    if (args.Length < 2 || args[0] != "--directory") return dirName;

    dirName = args[1];
    if (!Directory.Exists(dirName))
    {
        throw new ArgumentException($"Directory {dirName} does not exist");
    }
    return dirName;
}

static string CompressWithGzip(string text)
{
    var bytes = Encoding.UTF8.GetBytes(text);
    using var msi = new MemoryStream(bytes);
    using var mso = new MemoryStream();
    using (var gs = new GZipStream(mso, CompressionMode.Compress))
    {
        msi.CopyTo(gs);
    }
    return Convert.ToBase64String(mso.ToArray());
}
