using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RecorderTest
{
    public class AriClient
    {
        public AriClient()
        {
            Recordings = new RecordingsType();
        }

        public RecordingsType Recordings { get; }

        public class RecordingsType
        {
            /// <summary>
            /// Get the file associated with the stored recording.. 
            /// </summary>
            /// <param name="recordingName">The name of the recording</param>
            public byte[] GetStoredFile(string recordingName)
            {
                Random rnd = new Random();

                int throwExRand = rnd.Next(1, 11);

                if (throwExRand == 10)
                {
                    throw new HttpRequestException("", null, System.Net.HttpStatusCode.NotFound);
                }
                else if (throwExRand == 9)
                {
                    throw new HttpRequestException("", null, System.Net.HttpStatusCode.InternalServerError);
                }


                int length = rnd.Next(1, 1000);
                int timeMs = rnd.Next(100, 1001);

                const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
                string content = new string(Enumerable.Repeat(chars, length)
                    .Select(s => s[rnd.Next(s.Length)]).ToArray());

                Thread.Sleep(timeMs);

                return Encoding.ASCII.GetBytes(content);
            }

            /// <summary>
            /// Delete a stored recording.. 
            /// </summary>
            /// <param name="recordingName">The name of the recording</param>
            public void DeleteStored(string recordingName)
            {
                Random rnd = new Random();

                int throwExRand = rnd.Next(1, 21);

                if (throwExRand == 10)
                {
                    throw new HttpRequestException("", null, System.Net.HttpStatusCode.NotFound);
                }
                else if (throwExRand == 9)
                {
                    throw new HttpRequestException("", null, System.Net.HttpStatusCode.InternalServerError);
                }


                int timeMs = rnd.Next(100, 501);
                Thread.Sleep(timeMs);
            }
        }
    }
}
