using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RecorderTest
{
    /// <summary>
    /// File access wrapper interface to be able to perform unit tests.
    /// </summary>
    public interface IFileAccessor
    {
        Stream Create(string path);
    }
}
