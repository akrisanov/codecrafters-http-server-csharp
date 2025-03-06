using System.Net;
using System.Net.Sockets;
using System.Text;

Console.WriteLine("Starting server...");

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

        var (status, content) = requestPath switch
        {
            "" => ("200 OK", ""),
            // return URL parameter as the response content
            "echo" => ("200 OK", urlPath[1]),
            // return User-Agent header as the response content
            "user-agent" => ("200 OK", userAgentHeader),
            // otherwise, return 404 Not Found
            _ => ("404 Not Found", "")
        };

        // create HTTP response
        var responseBytes = Encoding.UTF8.GetBytes(
            $"HTTP/1.1 {status}\r\nConnection: keep-alive\r\nContent-Type: text/plain\r\nContent-Length: {content.Length}\r\n\r\n{content}");

        // send response to the client
        await client.SendAsync(responseBytes);
    }
}
