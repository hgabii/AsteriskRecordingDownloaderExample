using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace RecorderTest
{
    /// <summary>
    /// File access wrapper class.
    /// </summary>
    public class FileAccessor : IFileAccessor
    {
        public Stream Create(string path)
        {
            return new MemoryStream();
            //return File.Create(path);
        }
    }
}
