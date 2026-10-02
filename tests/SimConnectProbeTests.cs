using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using NocturneMemoryHelper;

// No MSFS installation and no memory-clearing operations are used by these tests.
internal static class SimConnectProbeTests
{
    private static int passed;
    private static int Main(string[] args)
    {
        try
        {
            Test("OPEN wire layout", delegate
            {
                byte[] open = SimConnectWire.Open(7, false);
                Equal(296u, SimConnectWire.UInt32(open, 0));
                Equal(4u, SimConnectWire.UInt32(open, 4));
                Equal(0xF0000001u, SimConnectWire.UInt32(open, 8));
                Equal(7u, SimConnectWire.UInt32(open, 12));
                Equal("Nocturne MSFS Memory Helper", SimConnectWire.String(open, 16, 256));
                Equal(0x46535800u, SimConnectWire.UInt32(open, 276));
                Equal(12u, SimConnectWire.UInt32(open, 280));
                Equal(282174u, SimConnectWire.UInt32(open, 288));
                Equal(61259u, SimConnectWire.UInt32(SimConnectWire.Open(1, true), 288));
            });
            Test("heartbeat wire layout", delegate
            {
                byte[] request = SimConnectWire.RequestSimState(9, 13);
                Equal(276u, SimConnectWire.UInt32(request, 0));
                Equal(0xF0000035u, SimConnectWire.UInt32(request, 8));
                Equal(9u, SimConnectWire.UInt32(request, 12));
                Equal(13u, SimConnectWire.UInt32(request, 16));
                Equal("Sim", SimConnectWire.String(request, 20, 256));
            });
            Test("fragmented stream reassembles packet", delegate
            {
                byte[] bytes = OpenReply("SunRise");
                using (Stream stream = new FragmentedStream(bytes))
                    Equal(308u, (uint)SimConnectWire.ReadPacket(stream, 1000).Length);
            });
            Test("short packet rejected", delegate { Reject(Header(11, 2), typeof(InvalidDataException)); });
            Test("oversized packet rejected", delegate { Reject(Header(0xFFFFFFFF, 2), typeof(InvalidDataException)); });
            Test("wrong protocol rejected", delegate
            {
                byte[] bytes = OpenReply("SunRise");
                Put(bytes, 4, 99);
                Reject(bytes, typeof(InvalidDataException));
            });
            Test("truncated packet rejected", delegate { Reject(Header(308, 2), typeof(EndOfStreamException)); });
            Test("missing simulator locks gate", delegate
            {
                using (SimConnectProbe probe = Probe("NMMH.Missing." + Guid.NewGuid(), true))
                {
                    probe.Update(0);
                    True(!probe.IsConnected);
                }
            });
            Test("missing pipe locks gate without blocking caller", delegate
            {
                using (SimConnectProbe probe = Probe("NMMH.Missing." + Guid.NewGuid(), true))
                {
                    Stopwatch elapsed = Stopwatch.StartNew();
                    probe.Update(Process.GetCurrentProcess().Id);
                    True(elapsed.ElapsedMilliseconds < 200);
                    Thread.Sleep(400);
                    True(!probe.IsConnected);
                }
            });
            if (args.Length > 0 && args[0] == "--protocol-only")
            {
                Console.WriteLine("PASS: " + passed + " protocol/nonconnection tests.");
                return 0;
            }
            Test("OPEN and matching heartbeats unlock; disconnect relocks", delegate
            {
                using (Server server = new Server(ServeHealthy))
                using (SimConnectProbe probe = Probe(server.Name, true))
                {
                    probe.Update(Process.GetCurrentProcess().Id);
                    Wait(delegate { return probe.IsConnected; });
                    True(server.Requests > 0);
                    server.ClosePipe();
                    Wait(delegate { return !probe.IsConnected; });
                }
            });
            Test("wrong server owner locks gate", delegate
            {
                using (Server server = new Server(ServeHealthy))
                using (SimConnectProbe probe = Probe(server.Name, false))
                {
                    probe.Update(Process.GetCurrentProcess().Id);
                    Wait(delegate { return probe.LastError.Contains("does not belong"); });
                    True(!probe.IsConnected);
                }
            });
            Test("MSFS 2020 cannot unlock 2024 gate", delegate
            {
                using (Server server = new Server(delegate(Server s)
                {
                    SimConnectWire.ReadPacket(s.Pipe, 1000);
                    Send(s.Pipe, OpenReply("KittyHawk"));
                    Thread.Sleep(300);
                }))
                using (SimConnectProbe probe = Probe(server.Name, true))
                {
                    probe.Update(Process.GetCurrentProcess().Id);
                    Wait(delegate { return probe.LastError.Contains("not MSFS 2024"); });
                    True(!probe.IsConnected);
                }
            });
            Test("OPEN alone never unlocks gate", delegate
            {
                using (Server server = new Server(delegate(Server s)
                {
                    SimConnectWire.ReadPacket(s.Pipe, 1000);
                    Send(s.Pipe, OpenReply("SunRise"));
                    SimConnectWire.ReadPacket(s.Pipe, 1000);
                    Thread.Sleep(600);
                }))
                using (SimConnectProbe probe = Probe(server.Name, true))
                {
                    probe.Update(Process.GetCurrentProcess().Id);
                    Thread.Sleep(150);
                    True(!probe.IsConnected);
                    Wait(delegate { return probe.LastError.Contains("timed out"); });
                    True(!probe.IsConnected);
                }
            });
            Test("wrong request ID never unlocks gate", delegate
            {
                using (Server server = new Server(delegate(Server s)
                {
                    SimConnectWire.ReadPacket(s.Pipe, 1000);
                    Send(s.Pipe, OpenReply("SunRise"));
                    SimConnectWire.ReadPacket(s.Pipe, 1000);
                    Send(s.Pipe, StateReply(999));
                    Thread.Sleep(600);
                }))
                using (SimConnectProbe probe = Probe(server.Name, true))
                {
                    probe.Update(Process.GetCurrentProcess().Id);
                    Wait(delegate { return probe.LastError.Contains("timed out"); });
                    True(!probe.IsConnected);
                }
            });
            Test("QUIT relocks gate", delegate
            {
                using (Server server = new Server(delegate(Server s)
                {
                    SimConnectWire.ReadPacket(s.Pipe, 1000);
                    Send(s.Pipe, OpenReply("SunRise"));
                    byte[] request = SimConnectWire.ReadPacket(s.Pipe, 1000);
                    Send(s.Pipe, StateReply(SimConnectWire.UInt32(request, 16)));
                    Thread.Sleep(150);
                    SimConnectWire.ReadPacket(s.Pipe, 1000);
                    Send(s.Pipe, Header(12, 3));
                    Thread.Sleep(150);
                }))
                using (SimConnectProbe probe = Probe(server.Name, true))
                {
                    probe.Update(Process.GetCurrentProcess().Id);
                    Wait(delegate { return probe.IsConnected; });
                    Wait(delegate { return !probe.IsConnected; });
                }
            });
            Test("simulator exit immediately cancels gate", delegate
            {
                using (Server server = new Server(ServeHealthy))
                using (SimConnectProbe probe = Probe(server.Name, true))
                {
                    probe.Update(Process.GetCurrentProcess().Id);
                    Wait(delegate { return probe.IsConnected; });
                    probe.Update(0);
                    True(!probe.IsConnected);
                }
            });
            Test("late OPEN cannot reactivate cancelled session", delegate
            {
                using (Server server = new Server(delegate(Server s)
                {
                    SimConnectWire.ReadPacket(s.Pipe, 1000);
                    s.OpenRead = true;
                    Thread.Sleep(150);
                    Send(s.Pipe, OpenReply("SunRise"));
                }))
                using (SimConnectProbe probe = Probe(server.Name, true))
                {
                    probe.Update(Process.GetCurrentProcess().Id);
                    Wait(delegate { return server.OpenRead; });
                    probe.Disconnect();
                    Thread.Sleep(300);
                    True(!probe.IsConnected);
                }
            });
            Test("disposed probe stays locked", delegate
            {
                SimConnectProbe probe = Probe("NMMH.Disposed." + Guid.NewGuid(), true);
                probe.Dispose();
                probe.Update(Process.GetCurrentProcess().Id);
                probe.Disconnect();
                probe.Dispose();
                True(!probe.IsConnected);
            });
            Test("unresponsive heartbeat relocks a previously connected gate", delegate
            {
                using (Server server = new Server(delegate(Server s)
                {
                    SimConnectWire.ReadPacket(s.Pipe, 1000);
                    Send(s.Pipe, OpenReply("SunRise"));
                    byte[] request = SimConnectWire.ReadPacket(s.Pipe, 1000);
                    Send(s.Pipe, StateReply(SimConnectWire.UInt32(request, 16)));
                    SimConnectWire.ReadPacket(s.Pipe, 1000);
                    Thread.Sleep(600);
                }))
                using (SimConnectProbe probe = Probe(server.Name, true))
                {
                    probe.Update(Process.GetCurrentProcess().Id);
                    Wait(delegate { return probe.IsConnected; });
                    Wait(delegate { return !probe.IsConnected; });
                }
            });
            Test("version mismatch retries on a fresh pipe connection", delegate
            {
                using (Server server = new Server(delegate(Server s)
                {
                    SimConnectWire.ReadPacket(s.Pipe, 1000);
                    byte[] rejected = new byte[24];
                    Buffer.BlockCopy(Header(24, 1), 0, rejected, 0, 12);
                    Put(rejected, 12, 5);
                    Send(s.Pipe, rejected);
                    if (Environment.OSVersion.Platform == PlatformID.Win32NT) s.Pipe.WaitForPipeDrain();
                    s.Pipe.Disconnect();
                    s.Pipe.WaitForConnection();
                    byte[] legacy = SimConnectWire.ReadPacket(s.Pipe, 1000);
                    Equal(10u, SimConnectWire.UInt32(legacy, 280));
                    Equal(61259u, SimConnectWire.UInt32(legacy, 288));
                    Send(s.Pipe, OpenReply("SunRise"));
                    byte[] request = SimConnectWire.ReadPacket(s.Pipe, 1000);
                    Send(s.Pipe, StateReply(SimConnectWire.UInt32(request, 16)));
                    Thread.Sleep(300);
                }))
                using (SimConnectProbe probe = Probe(server.Name, true))
                {
                    probe.Update(Process.GetCurrentProcess().Id);
                    Wait(delegate { return probe.IsConnected; });
                }
            });
            Test("process change requires a fresh verified connection", delegate
            {
                using (Server server = new Server(ServeHealthy))
                using (SimConnectProbe probe = Probe(server.Name, true))
                {
                    probe.Update(Process.GetCurrentProcess().Id);
                    Wait(delegate { return probe.IsConnected; });
                    probe.Update(Process.GetCurrentProcess().Id + 1);
                    True(!probe.IsConnected);
                }
            });
            Test("disconnect automatically reconnects after a new handshake", delegate
            {
                using (Server server = new Server(delegate(Server s)
                {
                    SimConnectWire.ReadPacket(s.Pipe, 1000);
                    Send(s.Pipe, OpenReply("SunRise"));
                    byte[] request = SimConnectWire.ReadPacket(s.Pipe, 1000);
                    Send(s.Pipe, StateReply(SimConnectWire.UInt32(request, 16)));
                    Thread.Sleep(150);
                    SimConnectWire.ReadPacket(s.Pipe, 1000);
                    Send(s.Pipe, Header(12, 3));
                    if (Environment.OSVersion.Platform == PlatformID.Win32NT) s.Pipe.WaitForPipeDrain();
                    s.Pipe.Disconnect();
                    s.Pipe.WaitForConnection();
                    ServeHealthy(s);
                }))
                using (SimConnectProbe probe = Probe(server.Name, true))
                {
                    probe.Update(Process.GetCurrentProcess().Id);
                    Wait(delegate { return probe.IsConnected; });
                    Wait(delegate { return !probe.IsConnected; });
                    Wait(delegate { return probe.IsConnected; });
                }
            });
            Test("truncated OPEN cannot unlock gate", delegate
            {
                using (Server server = new Server(delegate(Server s)
                {
                    SimConnectWire.ReadPacket(s.Pipe, 1000);
                    Send(s.Pipe, Header(12, 2));
                    Thread.Sleep(300);
                }))
                using (SimConnectProbe probe = Probe(server.Name, true))
                {
                    probe.Update(Process.GetCurrentProcess().Id);
                    Wait(delegate { return probe.LastError.Contains("complete SimConnect OPEN"); });
                    True(!probe.IsConnected);
                }
            });
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                Test("Windows verifies actual named-pipe server PID", delegate
                {
                    using (Server server = new Server(ServeHealthy))
                    using (SimConnectProbe probe = new SimConnectProbe(server.Name,
                        SimConnectProbe.VerifyOwner, 250, 50, 1000))
                    {
                        probe.Update(Process.GetCurrentProcess().Id);
                        Wait(delegate { return probe.IsConnected; });
                    }
                });
            Console.WriteLine("PASS: " + passed + " tests. No memory-clearing operations executed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAIL: " + ex);
            return 1;
        }
    }

    private static void ServeHealthy(Server server)
    {
        byte[] open = SimConnectWire.ReadPacket(server.Pipe, 1000);
        Equal(0xF0000001u, SimConnectWire.UInt32(open, 8));
        Send(server.Pipe, OpenReply("SunRise"));
        while (true)
        {
            byte[] request = SimConnectWire.ReadPacket(server.Pipe, 1000);
            Equal(0xF0000035u, SimConnectWire.UInt32(request, 8));
            server.Requests++;
            Send(server.Pipe, StateReply(SimConnectWire.UInt32(request, 16)));
        }
    }

    private static SimConnectProbe Probe(string name, bool ownsPipe)
    {
        return new SimConnectProbe(name,
            delegate(NamedPipeClientStream p, int id) { return ownsPipe; }, 250, 50, 1000);
    }

    private sealed class Server : IDisposable
    {
        internal readonly string Name = "NMMH.Tests." + Guid.NewGuid().ToString("N");
        internal readonly NamedPipeServerStream Pipe;
        internal volatile bool OpenRead;
        internal volatile int Requests;
        private readonly Thread thread;
        internal Server(Action<Server> handler)
        {
            Pipe = new NamedPipeServerStream(Name, PipeDirection.InOut, 1,
                PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            thread = new Thread(delegate()
            {
                try { Pipe.WaitForConnection(); handler(this); }
                catch (IOException) { }
                catch (ObjectDisposedException) { }
                catch (AggregateException ex)
                {
                    Exception cause = ex.GetBaseException();
                    if (!(cause is IOException) && !(cause is ObjectDisposedException)) throw;
                }
            });
            thread.IsBackground = true;
            thread.Start();
        }
        internal void ClosePipe() { Pipe.Dispose(); }
        public void Dispose() { ClosePipe(); thread.Join(1500); }
    }

    private sealed class FragmentedStream : MemoryStream
    {
        internal FragmentedStream(byte[] data) : base(data) { }
        public override int Read(byte[] buffer, int offset, int count)
        {
            return base.Read(buffer, offset, Math.Min(count, 3));
        }
        public override System.Threading.Tasks.Task<int> ReadAsync(byte[] buffer, int offset, int count,
            CancellationToken token)
        {
            return System.Threading.Tasks.Task.FromResult(Read(buffer, offset, count));
        }
    }

    private static byte[] Header(uint size, uint id)
    {
        byte[] result = new byte[12];
        Put(result, 0, size); Put(result, 4, 4); Put(result, 8, id);
        return result;
    }
    private static byte[] OpenReply(string name)
    {
        byte[] packet = new byte[308];
        Buffer.BlockCopy(Header(308, 2), 0, packet, 0, 12);
        byte[] application = Encoding.ASCII.GetBytes(name);
        Buffer.BlockCopy(application, 0, packet, 12, application.Length);
        return packet;
    }
    private static byte[] StateReply(uint requestId)
    {
        byte[] packet = new byte[284];
        Buffer.BlockCopy(Header(284, 15), 0, packet, 0, 12);
        Put(packet, 12, requestId);
        // Sim = 0 is a valid paused/menu state and must not lock a live session.
        return packet;
    }
    private static void Put(byte[] bytes, int offset, uint value)
    {
        for (int i = 0; i < 4; i++) bytes[offset + i] = (byte)(value >> (8 * i));
    }
    private static void Send(Stream stream, byte[] bytes)
    {
        // Fragmented replies exercise read framing instead of assuming one read per packet.
        for (int offset = 0; offset < bytes.Length; offset += 7)
            stream.Write(bytes, offset, Math.Min(7, bytes.Length - offset));
        stream.Flush();
    }
    private static void Reject(byte[] bytes, Type exception)
    {
        try
        {
            using (Stream stream = new MemoryStream(bytes)) SimConnectWire.ReadPacket(stream, 1000);
        }
        catch (Exception ex)
        {
            if (exception.IsInstanceOfType(ex)) return;
            throw;
        }
        throw new Exception("Expected " + exception.Name);
    }
    private static void Test(string name, Action action)
    {
        action(); passed++; Console.WriteLine("PASS " + name);
    }
    private static void Wait(Func<bool> condition)
    {
        Stopwatch watch = Stopwatch.StartNew();
        while (!condition())
        {
            if (watch.ElapsedMilliseconds > 2500) throw new Exception("Timed out waiting for test condition.");
            Thread.Sleep(10);
        }
    }
    private static void True(bool condition) { if (!condition) throw new Exception("Assertion failed."); }
    private static void Equal(uint expected, uint actual)
    {
        if (expected != actual) throw new Exception("Expected " + expected + ", got " + actual);
    }
    private static void Equal(string expected, string actual)
    {
        if (expected != actual) throw new Exception("Expected " + expected + ", got " + actual);
    }
}
