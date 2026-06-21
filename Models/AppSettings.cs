using System.Collections.Generic;
using System.Runtime.Serialization;

namespace PicDispatch.Models
{
    [DataContract]
    public class AppSettings
    {
        [DataMember]
        public List<string> SourceFolders { get; set; } = new List<string>();

        [DataMember]
        public List<TargetFolder> TargetFolders { get; set; } = new List<TargetFolder>();

        [DataMember]
        public double WindowWidth { get; set; } = 1180;

        [DataMember]
        public double WindowHeight { get; set; } = 760;
    }
}
