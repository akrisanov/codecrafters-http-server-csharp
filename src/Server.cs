using System.Net;
using System.Net.Sockets;
using System.Text;

Console.WriteLine("Starting server...");

using TcpListener server = new(IPAddress.Any, 4221); // TcpListener is unmanaged resource
server.Start();

// wait for a client to connect
using Socket client = server.AcceptSocket(); // Socket is unmanaged resource

// create HTTP response
string response = "HTTP/1.1 200 OK\r\n\r\n";
byte[] responseBytes = Encoding.UTF8.GetBytes(response);

// send response to the client
client.Send(responseBytes);

Console.WriteLine("Server stopped.");
