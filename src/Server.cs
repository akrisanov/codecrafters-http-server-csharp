using System.Net;
using System.Net.Sockets;
using System.Text;

Console.WriteLine("Starting server...");

TcpListener server = new(IPAddress.Any, 4221);
server.Start();
var client = server.AcceptSocket(); // wait for a client to connect

// create HTTP response
string response = "HTTP/1.1 200 OK\r\n\r\n";
byte[] responseBytes = Encoding.UTF8.GetBytes(response);

// send response to the client
client.Send(responseBytes);

client.Close();
server.Stop();

Console.WriteLine("Server stopped.");
