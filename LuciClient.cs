using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Collections;

namespace Luci
{
    public class SocketConnection
    {
        public MixedStream inOutStream;
        public bool isBusy;
       
        public SocketConnection(TcpClient tcpClient)
        {
            if (tcpClient != null)
            {
                NetworkStream networkStream = tcpClient.GetStream();
                inOutStream = new MixedStream(networkStream, Encoding.UTF8, 1024 * 1024 * 2);
                isBusy = false;
            }
        }
    }

    public enum State
    {
        Result,
        Error,
        Progress,
        Cancel,
        Run,
        None
    }
    public abstract class Attachment
    {
        public struct AttachmentInfo
        {
            public string checksum;
            public long length;
            public int position;
        }
        public static int AttachmentMemoryLimit = 1024 * 1024 * 500;
        public string format;
        public string name;
        public string crs;
        public AttachmentInfo attachment;
        protected Attachment()
        {
            attachment = new AttachmentInfo();
        }
        public Attachment(string format, string checksum, long length, int position = 0)
        {
            this.format = format;
            attachment = new AttachmentInfo
            {
                checksum = checksum.ToUpper(),
                length = length,
                position = position
            };
        }

        public abstract void write(MixedStream st);
        protected abstract string calcMyChecksum();
        public Attachment setLength(long length)
        {
            attachment.length = length;
            return this;
        }

        public Attachment setPosition(int position)
        {
            attachment.position = position;
            return this;
        }
        public Attachment setChecksum(string checksum)
        {
            string myChecksum = calcMyChecksum();
            if (myChecksum.Equals(checksum))
            {
                attachment.checksum = checksum.ToUpper();
                return this;
            }
            throw new InvalidDataException(string.Format("Checksums %s and %s dont match!", myChecksum, checksum));
        }
        public string getChecksum()
        {
            return attachment.checksum;
        }
        public long getLength()
        {
            return attachment.length;
        }
        public int getPosition()
        {
            return attachment.position;
        }
        public static Attachment read(MixedStream st, long length, int position)
        {
            if (length < AttachmentMemoryLimit)
            {
                byte[] data = new byte[length];
                st.Read(data);
                return new ArrayAttachment(data, position);
            }
            else
            {
                // TODO: read from stream into FileAttachment
            }
            return null;
        }
    }

    public class ArrayAttachment : Attachment
    {
        private byte[] data;
        internal ArrayAttachment(byte[] data, int position = 0) : base()
        {
            this.data = data;
            setLength(data.Length);
            setPosition(position);
        }
        public ArrayAttachment(string format, byte[] data, int position = 0) : base(format, calcChecksum(data), data.Length, position)
        {
            this.data = data;
        }
        public override void write(MixedStream st)
        {
            st.Write(data);
        }
        protected override string calcMyChecksum()
        {
            return calcChecksum(data);
        }
        private static string calcChecksum(byte[] data)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                return BitConverter.ToString(md5.ComputeHash(data)).Replace("-", string.Empty);
            }
        }
    }

    public class FileAttachment : Attachment
    {
        private string path;
        internal FileAttachment(long length, int position = 0)
        {
            setPosition(position);
            // TODO: create file with random name
        }
        public FileAttachment(string path, int position = 0) : base(Path.GetExtension(path), calcChecksum(path), (new FileInfo(path)).Length, position)
        {
            this.path = path;
        }
        public override void write(MixedStream st)
        {
            // TODO: wriate file attachment to stream
            throw new NotImplementedException();
        }
        protected override string calcMyChecksum()
        {
            return calcChecksum(path);
        }
        private static string calcChecksum(string path)
        {
            // TODO: calc checksum of file
            throw new NotImplementedException();
        }
    }

    public class Message
    {
        private Dictionary<string, Attachment> attachments = new Dictionary<string, Attachment>();
        private dynamic header;
        public Message(string header, Attachment[] rawAttachments)
        {
            this.header = initJ(JObject.Parse(header), rawAttachments);
        }
        private dynamic initJ(JObject jsonObject, Attachment[] rawAttachments = null)
        {
            DynamicDictionary dd = new DynamicDictionary();
            foreach(var item in jsonObject)
            {
                dd[item.Key] = initJValue(item.Value, rawAttachments);
            }
            return dd;
        }

        private dynamic initJValue(dynamic value, Attachment[] rawAttachments)
        {
            if (value is JObject)
            {
                if (value.attachment != null)
                {
                    Attachment a = value;
                    int pos = a.getPosition();
                    if (pos > 0)
                    {
                        Attachment raw = rawAttachments[pos - 1];
                        raw.setChecksum(a.getChecksum());
                        raw.format = a.format;
                        raw.name = a.name;
                        value = raw;
                    }
                }
                else if (value.format == null) value = initJ(value, rawAttachments);
                else if (((string)value.format).ToLower() == "geojson")
                {
                    value = new GeoJsonReader().Read<Geometry>(value.geometry);
                }
            }
            else if (value is JArray)
            {
                JArray array = value;
                List<object> values = new List<object>();
                foreach (var element in array)
                {
                    values.Add(initJValue(element, rawAttachments));
                }
                value = values;
            }
            return value;
        }
        public Message(object header)
        {
            this.header = initO(header);
        }
        public dynamic initO(dynamic o)
        {
            ExpandoObject exo = new ExpandoObject();
            IDictionary<string, object> exd = exo;
            foreach (var prop in o.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                exd[prop.Name] = initOValue(prop.GetValue(o, null));
            }
            return exo;
        }
        private dynamic initOValue(dynamic o)
        {
            if (o is Attachment)
            {
                Attachment a = (Attachment)o;
                if (!attachments.ContainsKey(a.getChecksum())) attachments[a.getChecksum()] = a;
                else o = attachments[a.getChecksum()];
            }
            else if (o is Geometry || o is FeatureCollection)
            {
                var jtw = new JTokenWriter();
                jtw.FloatFormatHandling = FloatFormatHandling.DefaultValue;
                new GeoJsonSerializer().Serialize(jtw, o);
                o =  new { format = "geojson", geometry = jtw.Token };
            }
            else if (o is IEnumerable<object>)
            {
                IList<object> l = new List<object>(o);
                for(int i = 0; i < l.Count(); i++)
                {
                    l[i] = initOValue(l[i]);
                }
                o = l;
            }
            else if (!(o is string) && !(o is Numeric) && !(o is bool))
                o = initO(o);
            return o;
        }
        public State getState()
        {
            if (header.result != null) return State.Result;
            if (header.error != null) return State.Error;
            if (header.progress != null) return State.Progress;
            if (header.cancel != null) return State.Cancel;
            if (header.run != null) return State.Run;
            return State.None;
        }
        public dynamic getHeader()
        {
            return header;
        }
        public IOrderedEnumerable<Attachment> getAttachments()
        {
            if (attachments.Values.Any(a => a.getPosition() == 0))
            {
                int i = 1;
                foreach (Attachment a in attachments.Values) a.setPosition(i++);
            }
            return attachments.Values.OrderBy(a => a.getPosition());
        }
        public int numAttachments()
        {
            return attachments.Count();
        }
        public long lenAttachments()
        {
            long sum = 0;
            attachments.Values.ToList().ForEach(a => sum += a.getLength());
            return sum;
        }
        public override string ToString()
        {
            return JsonConvert.SerializeObject(header);
        }
        public void writeTo(MixedStream st)
        {
            
            // important to access attachments before header is being BUILT:
            // attachment positions will be initialized
            IOrderedEnumerable<Attachment> attachmentList = getAttachments();
            string headerString = JsonConvert.SerializeObject(getHeader());
            byte[] headerBytes = Encoding.UTF8.GetBytes(headerString);
            byte[] lenHeader = BitConverter.GetBytes((long)headerBytes.Length);
            byte[] numAttachments = BitConverter.GetBytes((long)this.numAttachments());
            long attachmentLen = this.lenAttachments() + (this.numAttachments() + 1) * 8;
            byte[] lenAttachments = BitConverter.GetBytes((long)attachmentLen);

            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(lenHeader);
                Array.Reverse(lenAttachments);
                Array.Reverse(numAttachments);
            }
            st.Write(lenHeader);
            st.Flush();
            st.Write(lenAttachments);
            st.Flush();
            st.Write(headerBytes);
            st.Flush();            

            st.Write(numAttachments);
            st.Flush();

            if (this.numAttachments() > 0)
            {
                foreach (Attachment attachment in attachmentList)
                {
                    byte[] lenAttachment = BitConverter.GetBytes((long)attachment.getLength());
                    if (BitConverter.IsLittleEndian)
                        Array.Reverse(lenAttachment);

                    st.Write(lenAttachment);
                    st.Flush();
                    attachment.write(st);
                    st.Flush();
                }
            }
        }
        public static Message read(MixedStream st)
        {
            byte[] len1 = new byte[8];
            st.Read(len1);
            if (BitConverter.IsLittleEndian) Array.Reverse(len1);
            long lenHeader = BitConverter.ToInt64(len1, 0);

            byte[] len2 = new byte[8];
            st.Read(len2);
            if (BitConverter.IsLittleEndian) Array.Reverse(len2);
            long lenAttachments = BitConverter.ToInt64(len2, 0);

            byte[] msg = new byte[lenHeader];
            st.Read(msg);
            string header = Encoding.UTF8.GetString(msg, 0, (int)lenHeader);

            byte[] len3 = new byte[8];
            st.Read(len3);
            if (BitConverter.IsLittleEndian) Array.Reverse(len3);
            long numAttachments = BitConverter.ToInt64(len3, 0);

            Attachment[] rawAttachments = new Attachment[numAttachments];
            if (numAttachments > 0)
            {
                for(int i = 0; i < numAttachments; i++)
                {
                    byte[] attLen = new byte[8];
                    st.Read(attLen);
                    if (BitConverter.IsLittleEndian) Array.Reverse(attLen);
                    long lenAtt = BitConverter.ToInt64(attLen, 0);

                    rawAttachments[i] = Attachment.read(st, lenAtt, i+1);
                }
            }
            return new Message(header, rawAttachments);
        }
    }

    public class Client
    {
        private List<SocketConnection> socketPool = new List<SocketConnection>();
        private string _host;
        private int _port;
        private bool _enableThread = false;
        public struct GeoRef
        {
            public float lat;
            public float lon;
            public float alt;
            public string CRS;
            public string UNIT;
        }
        public struct ScenarioInfo
        {
            public int ScID;
            public long createdAt;
            public long lastModified;
            public string name;
            public GeoRef geoRef;
        }
        public void setEnableThread(bool enable)
        {
            _enableThread = enable;
        }
        public bool connect(String host, int port)
        {
            _host = host;
            _port = port;

            TcpClient socketForServer;
            try
            {
                socketForServer = new TcpClient(host, port);
                SocketConnection sc = new SocketConnection(socketForServer);
                socketPool.Add(sc);
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                "Failed to connect to server at {0}:{1}", host, port);
                return false;
            }

            return true;
        }

        private SocketConnection getFreeSocketConnection()
        {
            SocketConnection result = null;

            var query = socketPool.Where(s => s.isBusy == false);
            if (query != null && query.Any())
            {
                result = query.First();
            }
            else
            {
                TcpClient socketForServer = new TcpClient(_host, _port);
                result = new SocketConnection(socketForServer);

                socketPool.Add(result);
            }

            return result;
        }
        
        private async Task<Message> receiveMessage(SocketConnection sc)
        {
            if (_enableThread)
            {
                return await Task.Run(() => ignoreProgress(sc.inOutStream));
            }
            else return ignoreProgress(sc.inOutStream);
        }

        private Message ignoreProgress(MixedStream st)
        {
            Message message;
            State state;
            do
            {
                message = Message.read(st);
                state = message.getState();
            }
            while (state == State.Progress || state == State.None);
            return message;
        }

        public Message sendMessageAndReceiveResults(Message toSend)
        {
            Message answer;
            SocketConnection sc = getFreeSocketConnection();
            sc.isBusy = true;

            lock (sc.inOutStream)
            {
                toSend.writeTo(sc.inOutStream);
                Task<Message> la = receiveMessage(sc);
                answer = la.Result;
            }

            sc.isBusy = false;
            return answer;
        }

        public Message authenticate(string user, string password)
        {            
            return sendMessageAndReceiveResults(new Message(new {
                run = "authenticate",
                username = user,
                userpasswd = password
            }));
        }

        public Message registerService(string serviceName, string description, Dictionary<string, object> example, Dictionary<string, object> inputs = null, Dictionary<string, object> outputs = null)
        {
            dynamic message = new {
                run = "RemoteRegister",
                description = description,
                serviceName = serviceName,
                exampleCall = example
            };
            if (inputs != null) message.inputs = inputs;
            if (outputs != null) message.outputs = outputs;
            return sendMessageAndReceiveResults(new Message(message));
        }

        public void sendResult(dynamic result)
        {
            SocketConnection sc = getFreeSocketConnection();
            sc.isBusy = true;
            lock (sc.inOutStream)
            {
                new Message(new { result = result }).writeTo(sc.inOutStream);
            }
            sc.isBusy = false;
        }

        public Message getScenario(int scenarioId, dynamic options)
        {
            options.run = "scenario.geojson.Get";
            options.ScID = scenarioId;
            return  sendMessageAndReceiveResults(new Message(options));
        }

        public Message createScenario(string scenarioName, object geometry)
        {
            return sendMessageAndReceiveResults(new Message(new {
                run = "scenario.geojson.Create",
                name = scenarioName,
                geometry_input = geometry
            }));
        }

        public IList<string> getServiceList()
        {
            Message request = new Message(new  { run =  "ServiceList" });
            Message m = ifErrorThrow(sendMessageAndReceiveResults(request));
            var header = m.getHeader();
            List<object> listo = header.result.serviceNames;
            return listo.ConvertAll(o => o.ToString());
        }

        public IList<ScenarioInfo> getScenarioList()
        {
            Message m = ifErrorThrow(sendMessageAndReceiveResults(new Message(new { run = "scenario.GetList" })));
            List<object> scenarios = m.getHeader().result.scenarios;
            //return scenarios.ConvertAll(sc => (ScenarioInfo)scenarios);
            return null;
        }
        public Message updateScenario(int scenarioId, object geometry)
        {
            return sendMessageAndReceiveResults(new Message(new {
                run = "scenario.geojson.Update",
                ScID = scenarioId,
                geometry_input = geometry
            }));
        }
        private Message ifErrorThrow(Message m)
        {
            if (m.getState() == State.Error)
                throw new ApplicationException(m.getHeader().error);
            if (m.getState() == State.None)
                throw new ApplicationException("invalid message state");
            return m;
        }
    }
}

