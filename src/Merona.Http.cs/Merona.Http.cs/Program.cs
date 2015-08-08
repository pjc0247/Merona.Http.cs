using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Util;

using Merona;
using HttpMachine;

namespace Merona.Http.cs
{
    public class HttpMarshaler : Server.IMarshalContext
    {
        public class MyHttpDelegate : HttpMachine.IHttpParserHandler
        {
            private HttpMarshaler parent { get; set; }
            private Dictionary<String, String> data { get; set; }

            public MyHttpDelegate(HttpMarshaler parent)
            {
                this.parent = parent;
            }

            public void OnBody(HttpParser parser, ArraySegment<byte> data)
            {
            }

            public void OnFragment(HttpParser parser, string fragment)
            {
            }

            public void OnHeaderName(HttpParser parser, string name)
            {
            }

            public void OnHeadersEnd(HttpParser parser)
            {
                Console.WriteLine("end header");
            }

            public void OnHeaderValue(HttpParser parser, string value)
            {
            }

            public void OnMessageBegin(HttpParser parser)
            {
                Console.WriteLine("BEGIN");

                data = new Dictionary<String, String>();
            }

            public void OnMessageEnd(HttpParser parser)
            {
                Console.WriteLine("END");

                parent.processed.Enqueue(data);
            }

            public void OnMethod(HttpParser parser, string method)
            {
                Console.WriteLine(method);
            }

            public void OnQueryString(HttpParser parser, string queryString)
            {
                var kv = HttpUtility.ParseQueryString(queryString);
                
                foreach(var k in kv.AllKeys)
                {
                    Console.WriteLine("{0}:{1}", k, kv[k]);
                    data[k] = kv[k];
                }
            }

            public void OnRequestUri(HttpParser parser, string requestUri)
            {
                Console.WriteLine(requestUri);
            }
        }

        private HttpParser parser { get; set; }
        private Queue<Dictionary<String, String>> processed { get; set; }

        public HttpMarshaler()
        {
            this.parser = new HttpParser(new MyHttpDelegate(this));
            this.processed = new Queue<Dictionary<String, String>>();
        }

        protected override byte[] Serialize(CircularBuffer<Packet> buffer)
        {
            return null;
        }
        protected override Packet Deserialize(CircularBuffer<byte> buffer)
        {
            var parsed = parser.Execute(new ArraySegment<byte>(buffer.ToArray()));
            buffer.Skip(parsed);

            if (processed.Count > 0)
            {
                var data = processed.Dequeue();
                
                var id = Int32.Parse(data["packetId"]);
                
                var type = Packet.GetTypeById(id);

                if (type == null)
                    return null;

                var packet = Activator.CreateInstance(type);

                if (packet == null)
                    Console.WriteLine("packet is null");

                foreach(var field in data)
                {
                    var dest = type.GetField(field.Key);
                    var value = Convert.ChangeType(field.Value, dest.FieldType);
                    
                    dest.SetValue(packet, value);
                }

                Console.WriteLine(packet.GetType());

                return (Packet)packet;
            }
            else
                return null;
        }
    }

    [PacketId(1)]
    class FooPacket : Packet
    {
        public String foo;
        public String bar;
    }

    class FooService : Service
    {
        [Handler(typeof(FooPacket))]
        public void OnFoo(Session session, FooPacket packet)
        {
            Console.WriteLine("ON FOO {0} / {1}", packet.foo, packet.bar);
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var config = Config.defaults;
            config.marshalerType = typeof(HttpMarshaler);
            Server server = new Server(config);
            server.AttachService<FooService>(new FooService());
            server.Start();
        }
    }
}
