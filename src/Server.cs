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

// extract request target
var requestTarget = request.Split(' ')[1];

// create HTTP response
var status = string.IsNullOrEmpty(requestTarget) || requestTarget == "/" ? $"200 OK" : "404 Not Found";
var responseBytes = Encoding.UTF8.GetBytes($"HTTP/1.1 {status}\r\n\r\n");

// send response to the client
client.Send(responseBytes);

Console.WriteLine("Stopping server...");
