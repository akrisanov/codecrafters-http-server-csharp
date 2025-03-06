using System.Net;
using System.Net.Sockets;
using System.Text;

Dictionary<int, string> HttpStatusCodes = new()
{
    [200] = "OK",
    [404] = "Not Found",
};

Console.WriteLine("Starting server...");
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
        var requestTarget = requestMembers[0].Split(' ')[1]; // URL
        var urlPath = requestTarget.TrimStart('/').Split('/');
        var requestPath = urlPath[0]; // endpoint or page

        // parse headers
        var userAgentHeader = requestMembers.FirstOrDefault(p => p.StartsWith("User-Agent:"))?.Split(": ")[1] ?? "";

        // build a response
        var status = HttpStatusCodes[200];
        var headers = new Dictionary<string, object>
        {
            ["Connection"] = "keep-alive",
            ["Content-Type"] = "text/plain",
            ["Content-Length"] = 0,
        };
        var content = ""u8.ToArray();

        switch (requestPath)
        {
            case "": // root URL
                break;
            case "echo": // return URL parameter as the response content
                content = Encoding.UTF8.GetBytes(urlPath[1]);
                break;
            case "user-agent": // return User-Agent header as the response content
                content = Encoding.UTF8.GetBytes(userAgentHeader);
                break;
            case "files": // serve files from the specified directory --directory
                (status, content) = await ServeFileAsync(args[0], urlPath[1]);
                headers["Content-Type"] = "application/octet-stream";
                headers["Content-Length"] = content.Length;
                break;
            default:
                status = HttpStatusCodes[404];
                break;
        }

        // create HTTP response by combining status line, headers, and content
        var response = new StringBuilder();
        response.Append($"HTTP/1.1 {status}\r\n");
        foreach (var (key, value) in headers)
        {
            response.Append($"{key}: {value}\r\n");
        }
        response.Append("\r\n");
        response.Append(Encoding.UTF8.GetString(content));
        var responseBytes = Encoding.UTF8.GetBytes(response.ToString());

        // send response to the client
        await client.SendAsync(responseBytes);
    }
}

async Task<(string status, byte[] content)> ServeFileAsync(string fileDir, string fileName)
{
    var filePath = Path.Combine(fileDir, fileName);
    if (File.Exists(filePath))
    {
        var fileContent = await File.ReadAllBytesAsync(filePath);
        return (HttpStatusCodes[200], fileContent);
    }
    return (HttpStatusCodes[404], Array.Empty<byte>());
}
