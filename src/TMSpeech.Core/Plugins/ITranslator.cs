using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TMSpeech.Core.Plugins
{
    public interface ITranslator : IPlugin
    {
        string Translate(string text);
    }
}
