using System.Net;
using System.Net.Sockets;

Console.WriteLine("Starting server...");

TcpListener server = new(IPAddress.Any, 4221);
server.Start();
server.AcceptSocket(); // wait for a client
