using System;
using System.Collections.Generic;
using System.Text;

namespace BlobCompressionTools.Models
{
    public class BlobInfo
    {
        public string fileName;
        public string containerSource;
        public string containerTarget;
        public bool useManagedIdentity=true;

    }
}
