using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace RecorderTest
{
    /// <summary>
    /// Holds the information for recording, from StartCapturing
    /// to StopCapturing CORBA calls.
    /// </summary>
    public class RecordingItem
    {
        /// <summary>
        /// Unique name of the recording.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Bridge id on the Asterisk to record the call.
        /// </summary>
        [JsonIgnore]
        public string BridgeId { get; set; }

        /// <summary>
        /// File system directory to store the media file into.
        /// </summary>
        public string MediaDirectory { get; set; }

        /// <summary>
        /// File name to be used in the media directory.
        /// </summary>
        public string MediaFile { get; set; }

        /// <summary>
        /// Full path of the file to store recording in.
        /// </summary>
        [JsonIgnore]
        public string FilePath
        {
            get { return Path.Combine(MediaDirectory, MediaFile); }
        }
    }
}
