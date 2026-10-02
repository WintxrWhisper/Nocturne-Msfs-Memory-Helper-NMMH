using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace NocturneMemoryHelper
{
    // A small, read-only SimConnect wire client. No SDK or dynamically loaded DLL.
    // See docs/SimConnect.md for the packet format and verification boundaries.
    internal sealed class SimConnectProbe : IDisposable
    {
        internal const string DefaultPipeName = @"Microsoft Flight Simulator\SimConnect";
        private readonly object sync = new object();
        private readonly string pipeName;
        private readonly Func<NamedPipeClientStream, int, bool> verifyOwner;
        private readonly int responseTimeout;
        private readonly int heartbeatInterval;
        private readonly int retryInterval;
        private CancellationTokenSource session;
        private int processId;
        private bool connected;
        private bool disposed;
        private long lastReply;
        private string lastError = String.Empty;
        private string connectionInfo = String.Empty;

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out uint serverProcessId);

        internal SimConnectProbe() : this(DefaultPipeName, VerifyOwner, 10000, 5000, 3000) { }

        // Tests use their own local pipe and shortened deadlines; production never
        // bypasses ownership verification or connects to a remote server.
        internal SimConnectProbe(string name, Func<NamedPipeClientStream, int, bool> verifier,
            int timeout, int heartbeat, int retry)
        {
            pipeName = name;
            verifyOwner = verifier;
            responseTimeout = timeout;
            heartbeatInterval = heartbeat;
            retryInterval = retry;
        }

        internal string LastError { get { lock (sync) return lastError; } }
        internal string ConnectionInfo { get { lock (sync) return connectionInfo; } }
        internal bool IsConnected
        {
            get
            {
                lock (sync)
                    return connected && !disposed && session != null &&
                        !session.IsCancellationRequested &&
                        (Stopwatch.GetTimestamp() - lastReply) / (double)Stopwatch.Frequency <=
                        (responseTimeout + heartbeatInterval) / 1000.0;
            }
        }

        internal void Update(int simulatorProcessId)
        {
            lock (sync)
            {
                if (disposed) return;
                if (simulatorProcessId == processId && session != null) return;
                StopSession();
                processId = simulatorProcessId;
                if (processId <= 0)
                {
                    lastError = String.Empty;
                    return;
                }
                lastError = "waiting for SimConnect";
                CancellationTokenSource current = new CancellationTokenSource();
                session = current;
                int expectedProcess = processId;
                Thread thread = new Thread(delegate() { Run(current, expectedProcess); });
                thread.IsBackground = true;
                thread.Name = "NMMH SimConnect";
                thread.Start();
            }
        }

        internal static bool VerifyOwner(NamedPipeClientStream pipe, int expectedProcess)
        {
            uint owner;
            return GetNamedPipeServerProcessId(pipe.SafePipeHandle, out owner) &&
                owner == (uint)expectedProcess;
        }

        private void Run(CancellationTokenSource current, int expectedProcess)
        {
            CancellationToken token = current.Token;
            try
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        RunConnection(current, expectedProcess);
                    }
                    catch (Exception ex)
                    {
                        SetState(current, false, "SimConnect unavailable: " + ex.GetBaseException().Message);
                    }
                    if (token.WaitHandle.WaitOne(retryInterval)) break;
                }
            }
            finally
            {
                SetState(current, false, "waiting for SimConnect");
                current.Dispose();
            }
        }

        private void RunConnection(CancellationTokenSource current, int expectedProcess)
        {
            CancellationToken token = current.Token;
            using (NamedPipeClientStream pipe = new NamedPipeClientStream(
                ".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous))
            using (token.Register(delegate { try { pipe.Dispose(); } catch { } }))
            {
                pipe.Connect(1000);
                token.ThrowIfCancellationRequested();
                if (!verifyOwner(pipe, expectedProcess))
                    throw new IOException("local pipe does not belong to the detected simulator");

                uint sendId = 1;
                WritePacket(pipe, SimConnectWire.Open(sendId++), responseTimeout);
                byte[] open = ReadReply(pipe, responseTimeout, "OPEN reply");
                uint kind = SimConnectWire.UInt32(open, 8);
                if (kind == 1 && open.Length >= 24 && SimConnectWire.UInt32(open, 12) == 5)
                {
                    throw new IOException("protocol version rejected");
                }
                if (kind != 2 || open.Length < 308)
                    throw new IOException("expected a complete SimConnect OPEN reply");
                string application = SimConnectWire.String(open, 12, 256).Trim();
                if (!application.StartsWith("SunRise", StringComparison.OrdinalIgnoreCase) &&
                    !application.StartsWith("Microsoft Flight Simulator 2024", StringComparison.OrdinalIgnoreCase))
                    throw new IOException("the server is not MSFS 2024 (" + application + ")");
                lock (sync)
                {
                    if (Object.ReferenceEquals(session, current) && !current.IsCancellationRequested)
                        connectionInfo = application + ", client protocol 6, server protocol " +
                            SimConnectWire.UInt32(open, 4) + ", SimConnect " +
                            SimConnectWire.UInt32(open, 284) + "." + SimConnectWire.UInt32(open, 288) +
                            "." + SimConnectWire.UInt32(open, 292) + "." + SimConnectWire.UInt32(open, 296);
                }

                // OPEN alone is not sufficient: require a matching reply to our
                // own read-only request, including in menus or when paused.
                uint requestId = 1;
                while (!token.IsCancellationRequested)
                {
                    WritePacket(pipe, SimConnectWire.RequestSimState(sendId++, requestId), responseTimeout);
                    Stopwatch deadline = Stopwatch.StartNew();
                    bool replied = false;
                    while (!replied)
                    {
                        int remaining = responseTimeout - (int)deadline.ElapsedMilliseconds;
                        if (remaining <= 0) throw new TimeoutException("no SimConnect heartbeat reply");
                        byte[] response = ReadReply(pipe, remaining, "heartbeat reply");
                        uint id = SimConnectWire.UInt32(response, 8);
                        if (id == 3) throw new IOException("simulator closed the connection");
                        if (id == 1)
                            throw new IOException("SimConnect exception " +
                                (response.Length >= 24 ? SimConnectWire.UInt32(response, 12).ToString() : "(truncated)"));
                        if (id == 15 && response.Length >= 284 &&
                            SimConnectWire.UInt32(response, 12) == requestId)
                            replied = true;
                    }
                    token.ThrowIfCancellationRequested();
                    SetState(current, true, String.Empty);
                    requestId++;
                    if (token.WaitHandle.WaitOne(heartbeatInterval)) break;
                }
                SetState(current, false, "waiting for SimConnect");
            }
        }

        private static byte[] ReadReply(Stream stream, int timeout, string stage)
        {
            try { return SimConnectWire.ReadPacket(stream, timeout); }
            catch (Exception ex)
            {
                throw new IOException(stage + ": " + ex.GetBaseException().Message);
            }
        }

        private static void WritePacket(NamedPipeClientStream pipe, byte[] packet, int timeout)
        {
            Task write = pipe.WriteAsync(packet, 0, packet.Length);
            if (!write.Wait(timeout))
            {
                pipe.Dispose();
                ObserveFault(write);
                throw new TimeoutException("SimConnect write timed out");
            }
            write.GetAwaiter().GetResult();
        }

        internal static void ObserveFault(Task task)
        {
            task.ContinueWith(delegate(Task finished) { var ignored = finished.Exception; },
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
        }

        private void SetState(CancellationTokenSource current, bool value, string error)
        {
            lock (sync)
            {
                // Ignore replies/errors from a cancelled or replaced session.
                if (!Object.ReferenceEquals(session, current) || disposed || current.IsCancellationRequested) return;
                connected = value;
                lastError = error;
                if (value) lastReply = Stopwatch.GetTimestamp();
            }
        }

        private void StopSession()
        {
            connected = false;
            connectionInfo = String.Empty;
            CancellationTokenSource previous = session;
            session = null;
            processId = 0;
            if (previous != null)
            {
                try { previous.Cancel(); }
                catch (ObjectDisposedException) { }
            }
        }

        internal void Disconnect()
        {
            lock (sync) StopSession();
        }

        public void Dispose()
        {
            lock (sync)
            {
                disposed = true;
                StopSession();
            }
        }
    }

    internal static class SimConnectWire
    {
        private const uint Version = 6; // MSFS 2024 / SunRise, not FSX (4).
        private const int MaximumPacket = 4096; // Only OPEN and system-state traffic is requested.

        internal static uint UInt32(byte[] bytes, int offset)
        {
            if (offset < 0 || offset > bytes.Length - 4) throw new InvalidDataException("truncated field");
            return (uint)(bytes[offset] | bytes[offset + 1] << 8 |
                bytes[offset + 2] << 16 | bytes[offset + 3] << 24);
        }

        private static void PutUInt32(byte[] bytes, int offset, uint value)
        {
            for (int i = 0; i < 4; i++) bytes[offset + i] = (byte)(value >> (8 * i));
        }

        internal static string String(byte[] bytes, int offset, int count)
        {
            int end = offset;
            while (end < offset + count && bytes[end] != 0) end++;
            return Encoding.ASCII.GetString(bytes, offset, end - offset);
        }

        private static byte[] Packet(int length, uint operation, uint sendId)
        {
            byte[] result = new byte[length];
            PutUInt32(result, 0, (uint)length);
            PutUInt32(result, 4, Version);
            PutUInt32(result, 8, operation);
            PutUInt32(result, 12, sendId);
            return result;
        }

        internal static byte[] Open(uint sendId)
        {
            byte[] packet = Packet(296, 0xF0000001, sendId);
            byte[] name = Encoding.ASCII.GetBytes("Nocturne MSFS Memory Helper");
            Buffer.BlockCopy(name, 0, packet, 16, name.Length);
            // Reserved uint32, zero byte, three-byte SunRise identifier "RS\0".
            // This field is not the FSX "XSF" identifier.
            Buffer.BlockCopy(new byte[] { 0, 0, 0, 0, 0, 0x52, 0x53, 0 }, 0, packet, 272, 8);
            PutUInt32(packet, 280, 12);
            PutUInt32(packet, 284, 2);
            PutUInt32(packet, 288, 282174);
            PutUInt32(packet, 292, 999);
            return packet;
        }

        internal static byte[] RequestSimState(uint sendId, uint requestId)
        {
            byte[] packet = Packet(276, 0xF0000035, sendId);
            PutUInt32(packet, 16, requestId);
            byte[] state = Encoding.ASCII.GetBytes("Sim");
            Buffer.BlockCopy(state, 0, packet, 20, state.Length);
            return packet;
        }

        internal static byte[] ReadPacket(Stream stream, int timeout)
        {
            Stopwatch deadline = Stopwatch.StartNew();
            byte[] header = new byte[12];
            ReadExact(stream, header, 0, header.Length, timeout, deadline);
            uint size = UInt32(header, 0);
            if (size < 12 || size > MaximumPacket)
                throw new InvalidDataException("invalid SimConnect packet size");
            // This is the server's version, not necessarily our advertised
            // client version. Bound it to the understood header layouts.
            uint serverVersion = UInt32(header, 4);
            if (serverVersion < 4 || serverVersion > 6)
                throw new InvalidDataException("unsupported SimConnect server wire version " +
                    serverVersion + " (size " + size + ", reply " + UInt32(header, 8) + ")");
            byte[] packet = new byte[(int)size];
            Buffer.BlockCopy(header, 0, packet, 0, 12);
            ReadExact(stream, packet, 12, packet.Length - 12, timeout, deadline);
            return packet;
        }

        private static void ReadExact(Stream stream, byte[] buffer, int offset, int count,
            int timeout, Stopwatch deadline)
        {
            while (count > 0)
            {
                int remaining = timeout - (int)deadline.ElapsedMilliseconds;
                if (remaining <= 0) throw new TimeoutException("SimConnect response timed out");
                Task<int> read = stream.ReadAsync(buffer, offset, count);
                if (!read.Wait(remaining))
                {
                    stream.Dispose();
                    SimConnectProbe.ObserveFault(read);
                    throw new TimeoutException("SimConnect response timed out");
                }
                int received = read.GetAwaiter().GetResult();
                if (received == 0) throw new EndOfStreamException("SimConnect disconnected");
                offset += received;
                count -= received;
            }
        }
    }
}
