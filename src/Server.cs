using System.Net;
using System.Net.Sockets;
using System.Text;

Console.WriteLine("Starting server...");

using TcpListener server = new(IPAddress.Any, 4221); // TcpListener is unmanaged resource
server.Start();

// wait for a client to connect
using var client = server.AcceptSocket(); // Socket is unmanaged resource

// receive request from the client
var requestBytes = new byte[1024];
var bytesRead = client.Receive(requestBytes);
var request = Encoding.UTF8.GetString(requestBytes, 0, bytesRead);
var requestParts = request.Split(' ');

// extract request target
var requestTarget = requestParts[1];
var urlParams = requestTarget.TrimStart('/').Split('/');
var requestPath = urlParams[0];

var (status, content) = requestPath switch
{
    "" => ("200 OK", string.Empty),
    "echo" => ("200 OK", urlParams[1]),
    _ => ("404 Not Found", string.Empty)
};

// create HTTP response
var responseBytes = Encoding.UTF8.GetBytes(
    $"HTTP/1.1 {status}\r\nContent-Type: text/plain\r\nContent-Length: {content.Length}\r\n\r\n{content}");

// send response to the client
client.Send(responseBytes);

Console.WriteLine("Stopping server...");