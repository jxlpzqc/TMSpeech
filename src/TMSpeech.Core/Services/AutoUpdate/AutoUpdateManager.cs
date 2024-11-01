using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TMSpeech.Core.AutoUpdate;

class UpdateInfo
{
    public long Version { get; set; }
    public string NewFeature { get; set; }
}

class AutoUpdateManager
{
    public UpdateInfo? CheckUpdate()
    {
        // TODO: implement it
        return null;
    }
}