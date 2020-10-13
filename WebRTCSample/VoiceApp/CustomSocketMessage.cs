using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace VoiceApp
{
    public class CustomTypeResolver : JavaScriptTypeResolver
    {
        public override Type ResolveType(string id)
        {
            return Type.GetType(id);
        }

        public override string ResolveTypeId(Type type)
        {
            if (type == null)
            {
                throw new ArgumentNullException("type");
            }

            return type.FullName;
        }
    }

    public class CustomSocketMessage
    {
        public virtual int version { get; set; }
    }

    public class StartMedia : CustomSocketMessage
    {
        public string data { get; set; }
    }

    public class StopMedia : CustomSocketMessage
    {
        public string data { get; set; }
    }

}
