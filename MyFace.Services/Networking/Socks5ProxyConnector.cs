using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace MyFace.Services.Networking;

public sealed class Socks5ProxyConnector
{
    private readonly string _proxyHost;
    private readonly int _proxyPort;

    public Socks5ProxyConnector(string proxyHost, int proxyPort)
    {
        if (string.IsNullOrWhiteSpace(proxyHost))
        {
            throw new ArgumentException("Proxy host is required", nameof(proxyHost));
        }

        if (proxyPort <= 0 || proxyPort > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(proxyPort), "Proxy port must be between 1 and 65535.");
        }

        _proxyHost = proxyHost;
        _proxyPort = proxyPort;
    }

    public async ValueTask<Stream> ConnectAsync(DnsEndPoint target, CancellationToken cancellationToken)
    {
        if (target == null)
        {
            throw new ArgumentNullException(nameof(target));
        }

        TcpClient? tcpClient = null;
        NetworkStream? networkStream = null;
        try
        {
            tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(_proxyHost, _proxyPort, cancellationToken).ConfigureAwait(false);

            networkStream = new NetworkStream(tcpClient.Client, ownsSocket: true);

            await SendGreetingAsync(networkStream, cancellationToken).ConfigureAwait(false);
            await SendConnectRequestAsync(networkStream, target, cancellationToken).ConfigureAwait(false);
            tcpClient = null; // ownership transferred to NetworkStream
            return networkStream;
        }
        catch
        {
            networkStream?.Dispose();
            tcpClient?.Dispose();
            throw;
        }
        finally
        {
            tcpClient?.Dispose();
        }
    }

    private static async Task SendGreetingAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        byte[] greeting = { 0x05, 0x01, 0x00 };
        await stream.WriteAsync(greeting, cancellationToken).ConfigureAwait(false);

        byte[] response = new byte[2];
        await stream.ReadExactlyAsync(response, cancellationToken).ConfigureAwait(false);

        if (response[0] != 0x05 || response[1] != 0x00)
        {
            throw new InvalidOperationException("SOCKS5 proxy does not accept no-authentication method.");
        }
    }

    private static async Task SendConnectRequestAsync(NetworkStream stream, DnsEndPoint target, CancellationToken cancellationToken)
    {
        var hostBytes = Encoding.ASCII.GetBytes(target.Host);
        if (hostBytes.Length == 0 || hostBytes.Length > 255)
        {
            throw new InvalidOperationException("Target host name is invalid for SOCKS5.");
        }

        var request = new byte[4 + 1 + hostBytes.Length + 2];
        request[0] = 0x05; // version
        request[1] = 0x01; // connect
        request[2] = 0x00; // reserved
        request[3] = 0x03; // domain name
        request[4] = (byte)hostBytes.Length;
        Buffer.BlockCopy(hostBytes, 0, request, 5, hostBytes.Length);
        request[^2] = (byte)(target.Port >> 8);
        request[^1] = (byte)(target.Port & 0xFF);

        await stream.WriteAsync(request, cancellationToken).ConfigureAwait(false);

        byte[] response = new byte[4];
        await stream.ReadExactlyAsync(response, cancellationToken).ConfigureAwait(false);

        if (response[0] != 0x05)
        {
            throw new InvalidOperationException("Invalid SOCKS5 response header.");
        }

        if (response[1] != 0x00)
        {
            throw new InvalidOperationException($"SOCKS5 proxy failed to connect. Status: 0x{response[1]:X2}.");
        }

        int addressLength = response[3] switch
        {
            0x01 => 4,
            0x04 => 16,
            0x03 => await ReadAddressLengthAsync(stream, cancellationToken).ConfigureAwait(false),
            _ => throw new InvalidOperationException("Unsupported SOCKS5 address type in response.")
        };

        var discard = new byte[addressLength + 2];
        await stream.ReadExactlyAsync(discard, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<int> ReadAddressLengthAsync(Stream stream, CancellationToken cancellationToken)
    {
        byte[] lengthBuffer = new byte[1];
        await stream.ReadExactlyAsync(lengthBuffer, cancellationToken).ConfigureAwait(false);
        return lengthBuffer[0];
    }
}
